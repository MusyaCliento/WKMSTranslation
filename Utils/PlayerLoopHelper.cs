using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using System;
using System.Collections.Generic;

namespace WKMSTranslation.Utils
{
    public static class PlayerLoopHelper
    {
        public static void Inject(Type ownerType, Action updateAction)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type == typeof(Update))
                {
                    var subSystems = new List<PlayerLoopSystem>();
                    if (loop.subSystemList[i].subSystemList != null)
                    {
                        subSystems.AddRange(loop.subSystemList[i].subSystemList);
                    }
                    foreach (var sys in subSystems)
                    {
                        if (sys.type == ownerType) return;
                    }
                    var mySystem = new PlayerLoopSystem
                    {
                        type = ownerType,
                        updateDelegate = delegate { updateAction(); } 
                    };
                    subSystems.Add(mySystem);
                    loop.subSystemList[i].subSystemList = subSystems.ToArray();
                    PlayerLoop.SetPlayerLoop(loop);
                    UnityEngine.Debug.Log($"[WKMSTranslation] PlayerLoop injected successfully for {ownerType.Name}");
                    break;
                }
            }
        }
    }
}