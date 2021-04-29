using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Backend
{

    public class OAuthTokenListener
    {
        #region Private Members

        private static OAuthTokenListener instance = null;
        private static readonly object objLock = new object();

        private HttpListener listener;
        private string redirectUrl = String.Empty;

        #endregion

        #region Constructors

        public static OAuthTokenListener Instance
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
                        instance = new OAuthTokenListener();
                    }
                    return instance;
                }
            }
        }

        private OAuthTokenListener()
        {
        }

        #endregion

        #region Public Methods

        public event EventHandler<NameValueCollection> OnReceivedTokenData;

        public void StartListener(int port, string redirectURL)
        {
            this.redirectUrl = redirectURL;
            if (listener != null)
            {
                TryDisposeListener();
            }
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Listener started on port {port}");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} StartListener Exception: {ex}");
            }
        }

        public void StopListener()
        {
            TryDisposeListener();
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Listener stopped");
        }

        #endregion

        #region Private Methods

        private void ListenerCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            if (listener == null || !listener.IsListening)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Ignoring listener callback");
                return;
            }
            listener.BeginGetContext(new AsyncCallback(ListenerCallback), listener);
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Received request");

            HttpListenerContext context = listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            Logger.Instance.LogMessage(TracingLevel.INFO, $"{this.GetType()} Request: {request.RawUrl}");
            response.StatusCode = 302;
            response.Redirect(redirectUrl);
            response.OutputStream.Close();
            OnReceivedTokenData?.Invoke(this, request.QueryString);
        }

        private void TryDisposeListener()
        {
            if (listener == null)
            {
                return;
            }

            try
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }
            }
            catch { }
            listener = null;
        }
    }

    #endregion
}
