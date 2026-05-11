using Core.Event;
using Core.Pool;
using Core.Utils;

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
        
        private static PoolManager _poolManager;
        public static PoolManager Pool => _poolManager ??= new PoolManager();

        protected override void OnSingletonDestroyed()
        {
            _assetLoader = null;
            _mAudioManager = null;
            _poolManager.Unregister();
            _poolManager = null;
            _eventManager.Unregister();
            _eventManager = null;
        }
    }
}