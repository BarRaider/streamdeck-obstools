using BarRaider.ObsTools.Wrappers;
using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools
{
    internal class ServerManager
    {
        #region Private Members
        private const string TOKEN_FILE = "obs.dat";

        private static ServerManager instance = null;
        private static readonly object objLock = new object();

        private ServerInfo token;
        private object refreshTokensLock = new object();

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
            LoadToken();
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
            Logger.Instance.LogMessage(TracingLevel.INFO, "InitTokens");
            if (token == null || token.TokenLastRefresh < tokenCreateDate)
            {
                if (String.IsNullOrWhiteSpace(ip) || String.IsNullOrWhiteSpace(port))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "InitTokens: Token revocation!");
                }
                token = new ServerInfo() { Ip = ip, Port = port, Password = password, TokenLastRefresh = tokenCreateDate };
                SaveToken();
            }
            TokensChanged?.Invoke(this, new ServerInfoEventArgs(new ServerInfo() { Ip = token.Ip, Port = token.Port, Password = token.Password, TokenLastRefresh = token.TokenLastRefresh}));
        }

        #endregion

        #region Private Methods

        private void LoadToken()
        {
            try
            {
                string fileName = Path.Combine(System.AppContext.BaseDirectory, TOKEN_FILE);
                if (File.Exists(fileName))
                {
                    using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        var formatter = new BinaryFormatter();
                        token = (ServerInfo)formatter.Deserialize(stream);
                        if (token == null)
                        {
                            Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to load tokens, deserialized token is null");
                            return;
                        }
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Token initialized. Last refresh date was: {token.TokenLastRefresh}");
                    }
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to load tokens, token file does not exist: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Exception loading tokens: {ex}");
            }
        }

        private void SaveToken()
        {
            try
            {
                var formatter = new BinaryFormatter();
                using (var stream = new FileStream(Path.Combine(System.AppContext.BaseDirectory, TOKEN_FILE), FileMode.Create, FileAccess.Write))
                {

                    formatter.Serialize(stream, token);
                    stream.Close();
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"New token saved. Last refresh date was: {token.TokenLastRefresh}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Exception saving tokens: {ex}");
            }
        }

        #endregion
    }
}
