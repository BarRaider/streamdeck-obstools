﻿using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Backend
{
    internal class ServerManager
    {
        #region Private Members
        private static ServerManager instance = null;
        private static readonly object objLock = new object();

        private ServerInfo token;
        private GlobalSettings global;

        #endregion

        #region Public Members

        public event EventHandler<ServerInfoEventArgs> TokensChanged;
        #endregion

        #region Constructors

        public static ServerManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new ServerManager();
                    }
                    return instance;
                }
            }
        }

        private ServerManager()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
            
            // Initialize other managers
            SmartSceneSwitcherManager.Instance.Initialize();
        }

        #endregion

        #region Public Methods

        public bool ServerInfoExists
        {
            get
            {
                return (token != null && !string.IsNullOrWhiteSpace(token.Ip) && !string.IsNullOrWhiteSpace(token.Port));
            }
        }

        public ServerInfo ServerInfo
        {
            get
            {
                if (!ServerInfoExists)
                {
                    return null;
                }
                return new ServerInfo() { Ip = token.Ip, Port = token.Port, Password = token.Password, TokenLastRefresh = token.TokenLastRefresh };
            }
        }

        internal void InitTokens(string ip, string port, string password, DateTime tokenCreateDate)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "ServerManager: InitTokens");
            if (token == null || token.TokenLastRefresh < tokenCreateDate)
            {
                if (String.IsNullOrWhiteSpace(ip) || String.IsNullOrWhiteSpace(port))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "ServerManager InitTokens: Token revocation!");
                    if (global != null)
                    {
                        global.TwitchToken = null;
                    }
                }
                token = new ServerInfo() { Ip = ip, Port = port, Password = password, TokenLastRefresh = tokenCreateDate };
                SaveToken();
            }
            TokensChanged?.Invoke(this, new ServerInfoEventArgs(new ServerInfo() { Ip = token.Ip, Port = token.Port, Password = token.Password, TokenLastRefresh = token.TokenLastRefresh}));
        }

        #endregion

        #region Private Methods

        private void LoadToken(ServerInfo serverInfo)
        {
            try
            {
                if (serverInfo == null)
                {

                    Logger.Instance.LogMessage(TracingLevel.ERROR, "ServerManager: Failed to load tokens, deserialized serverInfo is null");
                    return;
                }

                token = new ServerInfo()
                {
                    Ip = serverInfo.Ip,
                    Password = serverInfo.Password,
                    Port = serverInfo.Port,
                    TokenLastRefresh = serverInfo.TokenLastRefresh

                };
                Logger.Instance.LogMessage(TracingLevel.INFO, $"ServerManager: Token initialized. Last refresh date was: {token.TokenLastRefresh}. Server: {token?.Ip}:{token?.Port}");
                TokensChanged?.Invoke(this, new ServerInfoEventArgs(new ServerInfo() { Ip = token.Ip, Port = token.Port, Password = token.Password, TokenLastRefresh = token.TokenLastRefresh }));
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ServerManager: Exception loading tokens: {ex}");
            }
        }

        private void SaveToken()
        {
            try
            {
                if (global == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "ServerManager: Global Settings is null, creating new instance");
                    global = new GlobalSettings();
                }

                // Set token in Global Settings
                if (token == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "ServerManager: Saving null token to Global Settings");
                    global.ServerInfo = null;
                }
                else
                {
                    global.ServerInfo = new ServerInfo()
                    {
                        Ip = token.Ip,
                        Password = token.Password,
                        Port = token.Port,
                        TokenLastRefresh = token.TokenLastRefresh
                    };
                    Logger.Instance.LogMessage(TracingLevel.INFO, "ServerManager saving serverinfo to global");
                }
                GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(global));
                Logger.Instance.LogMessage(TracingLevel.INFO, $"ServerManager: New token saved. Last refresh date was: {token.TokenLastRefresh} Server: {token?.Ip}:{token?.Port}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"ServerManager: Exception saving tokens: {ex}");
            }
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                global = payload.Settings.ToObject<GlobalSettings>();
                LoadToken(global.ServerInfo);
            }
        }

        #endregion
    }
}
