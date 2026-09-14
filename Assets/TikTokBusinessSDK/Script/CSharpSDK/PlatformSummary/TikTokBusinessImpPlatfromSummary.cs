namespace SDK
{
    
    public class TikTokBusinessImpPlatfromSummary
    {
        private static TikTokBusinessImpPlatfromSummary _instance;

        private ITikTokBusiness _iTikTokBusiness;

        private TikTokBusinessImpPlatfromSummary()
        {
#if UNITY_ANDROID
            _iTikTokBusiness = new AndroidTikTokBusinessImp();
#elif UNITY_IOS
            _iTikTokBusiness = new IOSTikTokBusinessImp();
#elif UNITY_EDITOR
            _iTikTokBusiness = new EditorTikTokBusinessImp();
#else
            // LOCAL FIX to vendor code. Upstream used EditorTikTokBusinessImp for every platform
            // that is not Android or iOS, but that class is itself wrapped in #if UNITY_EDITOR, so
            // it does not exist in a player. A desktop player build therefore failed to compile —
            // which broke the Windows screenshot harness, and only that: Android and iOS take their
            // own branches above and are byte-for-byte unaffected.
            //
            // Left null rather than stubbed because nothing can reach it. The kit's TikTok service
            // is entirely inside #if GAMEKIT_TIKTOK, and that define is set for Android and iPhone
            // only — never Standalone.
            //
            // Re-importing the TikTok SDK will overwrite this file and bring the bug back.
            _iTikTokBusiness = null;
#endif
        }

        public static TikTokBusinessImpPlatfromSummary Instance()
        {
            return _instance ?? (_instance = new TikTokBusinessImpPlatfromSummary());
        }

        // 获取窗体的实现类
        public ITikTokBusiness GetTiktokBusiness()
        {
            return _iTikTokBusiness;
        }
    }
}