using BarRaider.ObsTools.Properties;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Backend
{
    internal static class CommonFunctions
    {
        internal static Task<List<SceneBasicInfo>> FetchScenesAndActiveCaption()
        {
            return Task.Run(async () =>
            {
                var scenesList = new List<SceneBasicInfo>
                {
                    new SceneBasicInfo
                    {
                        Name = Constants.ACTIVE_SCENE_CAPTION
                    }
                };
                int retries = 40;

                while (!OBSManager.Instance.IsConnected && retries > 0)
                {
                    retries--;
                    await Task.Delay(250);
                }

                var scenes = OBSManager.Instance.GetAllScenes();
                if (scenes != null && scenes.Scenes != null)
                {
                    scenesList.AddRange(scenes.Scenes.OrderBy(s => s.Name).ToList());
                }
                return scenesList;
            });
        }
    }
}
