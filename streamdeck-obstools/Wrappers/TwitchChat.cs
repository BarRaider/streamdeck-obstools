using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Text;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using System.Linq;

namespace ChatPager.Twitch
{
    public class TwitchChat
    {

        #region Private Members
        private const string DEFAULT_CHAT_MESSAGE = "Hey, {USERNAME}, I am now getting paged...! (Get a pager for your Elgato Stream Deck at https://barraider.github.io )";

        private static TwitchChat instance = null;
        private static readonly object objLock = new object();

        private const string PAGE_COMMAND = "page";

        private TwitchClient client;
        private TwitchToken token = null;
        private int pageCooldown;
        private DateTime lastPage;
        private List<string> allowedPagers;
        private DateTime lastConnectAttempt;
        private object initLock = new object();

        #endregion

        #region Constructors

        public static TwitchChat Instance
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
                        instance = new TwitchChat();
                    }
                    return instance;
                }
            }
        }

        #endregion

        #region Public Members

        public event EventHandler<PageRaisedEventArgs> PageRaised;

        public bool IsConnected
        {
            get
            {
                return client.IsConnected;
            }
        }

        public string ChatMessage { get; private set; }

        #endregion


        private TwitchChat()
        {
            ChatMessage = DEFAULT_CHAT_MESSAGE;
            ResetClient();
            TwitchTokenManager.Instance.TokensChanged += Instance_TokensChanged;
            token = TwitchTokenManager.Instance.GetToken();

            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += Instance_OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }

        private void Instance_OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload e)
        {
            
        }

        #region Public Methods

        public void Initalize(int pageCooldown, List<string> allowedPagers)
        {
            lock (initLock)
            {
                try
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchChat: Initalizing");
                    if (allowedPagers != null)
                    {
                        this.allowedPagers = allowedPagers.Select(x => x.ToLowerInvariant()).ToList();
                    }
                    this.pageCooldown = pageCooldown;

                    if (!client.IsConnected)
                    {
                        Connect(DateTime.Now);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"TwitchChat: Initalize exception {ex}");
                }
            }
        }

        public void SetChatMessage(string message)
        {
            ChatMessage = message;
        }

        #endregion

        #region Private Methods

        private void Connect(DateTime connectRequestTime)
        {
            lock (objLock)
            {
                try
                {
                    if ((lastConnectAttempt > connectRequestTime) || ((connectRequestTime - lastConnectAttempt).TotalSeconds < 2)) // Prevent spamming Twitch
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"TwitchChat: Connected recently");
                        return;
                    }
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"TwitchChat: Connect called");
                    Disconnect(); // Disconnect if already conected with previous credentials

                    if (token == null || String.IsNullOrWhiteSpace(token.Token))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchChat: Cannot connect, invalid token");
                        return;
                    }

                    if (TwitchTokenManager.Instance.User == null || String.IsNullOrWhiteSpace(TwitchTokenManager.Instance.User.UserName))
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchChat: Cannot connect, invalid user object");
                        return;
                    }

                    Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchChat: Attempting to connect");
                    string username = TwitchTokenManager.Instance.User.UserName;
                    ConnectionCredentials credentials = new ConnectionCredentials(username, $"oauth:{token.Token}");
                    ResetClient();
                    client.Initialize(credentials, username);
                    client.Connect();
                    lastConnectAttempt = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"TwitchChat: Connect exception {ex}");
                }
            }
        }

        private void Disconnect()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "TwitchChat: Attempting to disconnect");
            if (client != null && client.IsConnected)
            {
                client.Disconnect();
            }
        }

        private void ParseCommand(ChatCommand cmd)
        {
            var msg = cmd.ChatMessage;
            if (cmd.CommandText.ToLowerInvariant() == PAGE_COMMAND)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{msg.DisplayName} requested a page");
                if (PageRaised != null)
                {
                    if ((DateTime.Now - lastPage).TotalSeconds > pageCooldown)
                    {
                        if (allowedPagers == null || allowedPagers.Count == 0 || allowedPagers.Contains(msg.DisplayName.ToLowerInvariant()))
                        {
                            lastPage = DateTime.Now;
                            PageRaised?.Invoke(this, new PageRaisedEventArgs(cmd.ArgumentsAsString));

                            if (!String.IsNullOrWhiteSpace(ChatMessage))
                            {
                                string chatMessage = ChatMessage.Replace("{USERNAME}", $"@{msg.DisplayName}");
                                client.SendMessage(msg.Channel, chatMessage);
                            }
                        }
                        else
                        {
                            Logger.Instance.LogMessage(TracingLevel.INFO, $"Cannot page, user {msg.DisplayName} is not allowed to page");
                        }
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Cannot page, cooldown enabled");
                    }
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Cannot page, no plugin is currently enabled");
                }
            }
        }

        private void Client_OnConnected(object sender, TwitchLib.Client.Events.OnConnectedArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Connected to chat room: {e.AutoJoinChannel}");
        }

        private void Client_OnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Disconnected from chat room");
        }

        private void Instance_TokensChanged(object sender, TwitchTokenEventArgs e)
        {
            token = e.Token;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Tokens changed, reconnecting");
            Connect(DateTime.Now);
        }

        private void ResetClient()
        {
            if (client != null)
            {
                client.OnConnected -= Client_OnConnected;
                client.OnDisconnected -= Client_OnDisconnected;
                client.OnChatCommandReceived -= Client_OnChatCommandReceived;
                client.OnUserJoined -= Client_OnUserJoined;
                client.OnUserLeft -= Client_OnUserLeft;
                client.OnConnectionError -= Client_OnConnectionError;
                client.OnError -= Client_OnError;
                
            }
            client = null;
            client = new TwitchClient();
            client.OnConnected += Client_OnConnected;
            client.OnDisconnected += Client_OnDisconnected;
            client.OnChatCommandReceived += Client_OnChatCommandReceived;
            client.OnUserJoined += Client_OnUserJoined;
            client.OnUserLeft += Client_OnUserLeft;
            client.OnConnectionError += Client_OnConnectionError;
            client.OnError += Client_OnError;

            // TODO -= these
            client.OnCommunitySubscription += Client_OnCommunitySubscription;
            client.OnHostingStarted += Client_OnHostingStarted;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnRaidNotification += Client_OnRaidNotification;
            client.OnUserStateChanged += Client_OnUserStateChanged;
            client.OnWhisperReceived += Client_OnWhisperReceived;

        }

        private void Client_OnWhisperReceived(object sender, TwitchLib.Client.Events.OnWhisperReceivedArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"***Whisper: {e.WhisperMessage.Username}: {e.WhisperMessage.Message}");
        }

        private void Client_OnUserStateChanged(object sender, TwitchLib.Client.Events.OnUserStateChangedArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"***UserState: {e.UserState.DisplayName} {e.UserState.UserType}");
        }

        private void Client_OnRaidNotification(object sender, TwitchLib.Client.Events.OnRaidNotificationArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"***Raid: {e.RaidNotificaiton.DisplayName}");
        }

        private void Client_OnNewSubscriber(object sender, TwitchLib.Client.Events.OnNewSubscriberArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"***NewSubscriber: {e.Subscriber.DisplayName}");
        }

        private void Client_OnHostingStarted(object sender, TwitchLib.Client.Events.OnHostingStartedArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"***Hosting Started: {e.HostingStarted.HostingChannel}");
        }

        private void Client_OnCommunitySubscription(object sender, TwitchLib.Client.Events.OnCommunitySubscriptionArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"***CommunitySubscription: {e.GiftedSubscription.DisplayName}");
        }

        private void Client_OnError(object sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"TwitchChat Error: {e.Exception}");
        }

        private void Client_OnConnectionError(object sender, TwitchLib.Client.Events.OnConnectionErrorArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"TwitchChat Connection Error: {e.Error.Message}");
        }

        private void Client_OnChatCommandReceived(object sender, TwitchLib.Client.Events.OnChatCommandReceivedArgs e)
        {
            ParseCommand(e.Command);
        }

        private void Client_OnUserLeft(object sender, TwitchLib.Client.Events.OnUserLeftArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"User left channel: {e.Username}");
            client.SendWhisper("BarRaider", $"{e.Username} left channel");
        }

        private void Client_OnUserJoined(object sender, TwitchLib.Client.Events.OnUserJoinedArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"User joined channel: {e.Username}");
            client.SendWhisper("BarRaider", $"{e.Username} joined channel");
        }

        #endregion


    }
}
