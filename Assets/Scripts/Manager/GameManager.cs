using Core.Event;
using Core.Utils;
using UnityEngine;

namespace Manager
{
    public class GameManager : Singleton<GameManager>
    {
        private static EventManager _eventManager;
        public static EventManager Event => _eventManager ??= new EventManager();
        
        private static AssetLoader _assetLoader;
        public static AssetLoader AssetLoader => _assetLoader ??= new AssetLoader();

        private static MAudioManager _mAudioManager;
        public static MAudioManager mAudio => _mAudioManager ??= new MAudioManager(null, null, null);
    }
}