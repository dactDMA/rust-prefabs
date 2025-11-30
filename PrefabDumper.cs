using System;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Prefab Dumper", "SnekSnake", "0.6.9")]
    [Description("Dump All Prefabs")]
    public class PrefabDumper : CovalencePlugin
    {
        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandPrefab"] = "prefab",
                ["ResultsSaved"] = "Prefab results saved to logs/{0}.txt",
                ["UsagePrefab"] = "Usage: {0} prefab <all|header>",
                ["NotAllowed"] = "You are not allowed to use this command"
            }, this);
        }

        #endregion Localization

        #region Initializaton

        private const string permissionUse = "prefabdumper.dump";

        private Dictionary<string, UnityEngine.Object> files;
        private GameManifest.PooledString[] manifest;

        private void OnServerInitialized()
        {
            AddLocalizedCommand(nameof(CommandPrefab));
            permission.RegisterPermission(permissionUse, this);

            files = FileSystem.Backend.cache;
            manifest = GameManifest.Current.pooledStrings;
        }

        #endregion Initialization
        
        #region Dumper
        
        private void DumpAll()
        {
            LogToFile("all", "// Prefab Sniffer Updated Version ~ https://www.unknowncheats.me/forum/members/4932472.html", this);
            foreach (GameManifest.PooledString asset in manifest)
                LogToFile("all", $"{asset.hash} - {asset.str}", this);
        }
        class NameSpaceContainer
        {
            public string name;
            public Content[] content;
            public List<NameSpaceContainer> children;
        }
        struct Content
        {
            public string name;
            public string hash;
        }
        private void DumpPrefab()
        {
            var namespaces = new List<NameSpaceContainer>();

            foreach (var asset in manifest)
            {
                if (!asset.str.EndsWith(".prefab"))
                    continue;

                var parts = asset.str.Split('/');
                AddToNamespace(namespaces, parts, 0, asset);
            }
            LogToFile("prefabs", "#pragma once", this);
            LogToFile("prefabs", "#include <cstdint>", this);
            LogToFile("prefabs", "// Prefab Sniffer Updated Version ~ https://www.unknowncheats.me/forum/members/4932472.html", this);
            foreach (var ns in namespaces)
                WriteNamespace(ns, 0);
        }
        
        private void AddToNamespace(List<NameSpaceContainer> namespaces, string[] parts, int index, GameManifest.PooledString asset)
        {
            if (index >= parts.Length)
                return;

            var currentName = parts[index];
            var existingNamespace = namespaces.FirstOrDefault(ns => ns.name == currentName);

            if (existingNamespace == null)
            {
                var newNamespace = new NameSpaceContainer
                {
                    name = currentName,
                    content = index == parts.Length - 1
                        ? new[] { new Content { name = asset.str, hash = asset.hash.ToString() } }
                        : Array.Empty<Content>(),
                    children = new List<NameSpaceContainer>() 
                };

                namespaces.Add(newNamespace);
                AddToNamespace(newNamespace.children, parts, index + 1, asset);
            }
            else
                if (index == parts.Length - 1)
                {
                    var contentList = existingNamespace.content.ToList();
                    contentList.Add(new Content { name = asset.str, hash = asset.hash.ToString() });
                    existingNamespace.content = contentList.ToArray();
                }
                else
                    AddToNamespace(existingNamespace.children, parts, index + 1, asset);
        }

        private void WriteNamespace(NameSpaceContainer ns, int indentLevel)
        {
            var indent = new string('\t', indentLevel);
            
            
            var namespaceName = ns.name.Replace(" ", "_").Replace(".", "_").Replace("-", "_");
            if (char.IsDigit(namespaceName[0]))
                namespaceName = $"_{namespaceName}";
            if (namespaceName == "static")
                namespaceName = $"_{namespaceName}";

            if (ns.name.EndsWith(".prefab"))
            {
                var constantName = ns.name.Replace(".prefab", "").Replace('.', '_').Replace('-', '_');
                if (char.IsDigit(constantName[0]))
                    constantName = $"_{constantName}";
                
                LogToFile("prefabs", $"{indent}constexpr uint32_t {constantName}_p = {ns.content[0].hash}; // {ns.content[0].name}", this);
                return;
            }

            LogToFile("prefabs", $"{indent}namespace {namespaceName}", this);
            LogToFile("prefabs", $"{indent}{{", this);

            if (ns.children != null)
                foreach (var child in ns.children)
                    WriteNamespace(child, indentLevel + 1);

            LogToFile("prefabs", $"{indent}}}", this);
        }
        
        #endregion Dumper

        #region Commands

        private void CommandPrefab(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length == 0)
            {
                Message(player, "UsagePrefab", command);
                return;
            }

            List<GameManifest.PooledString> resourcesList = new List<GameManifest.PooledString>();
            string argName = "";

            switch (args[0].ToLower())
            {
                case "header":
                    DumpPrefab();
                    Message(player, "ResultsSaved",
                        $"{Name}/{Name.ToLower()}/{args[0].ToLower()}-{DateTime.Now:yyyy-MM-dd}.txt");
                    break;
                case "all":
                    DumpAll();
                    Message(player, "ResultsSaved",
                        $"{Name}/{Name.ToLower()}/{args[0].ToLower()}-{DateTime.Now:yyyy-MM-dd}.txt");
                    break;
                default:
                    Message(player, "UsagePrefab", command);
                    break;
            }
        }

        #endregion Commands

        #region Helpers

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers
    }
}