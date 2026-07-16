using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ColorBlocks.Services
{
    public interface IHapticsService
    {
        void Light();
        void Soft();
        void Heavy();
    }

    public static class HapticsServiceFactory
    {
        public static IHapticsService Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosHapticsService();
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return new AndroidHapticsService();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Android haptics initialization failed and will remain disabled: {exception.Message}");
                return new NoOpHapticsService();
            }
#else
            return new NoOpHapticsService();
#endif
        }
    }

    internal sealed class NoOpHapticsService : IHapticsService
    {
        public void Light() { }
        public void Soft() { }
        public void Heavy() { }
    }

#if UNITY_IOS && !UNITY_EDITOR
    internal sealed class IosHapticsService : IHapticsService
    {
        [DllImport("__Internal")] private static extern void CB_HapticLight();
        [DllImport("__Internal")] private static extern void CB_HapticSoft();
        [DllImport("__Internal")] private static extern void CB_HapticHeavy();

        public void Light() => CB_HapticLight();
        public void Soft() => CB_HapticSoft();
        public void Heavy() => CB_HapticHeavy();
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class AndroidHapticsService : IHapticsService
    {
        private readonly AndroidJavaObject _vibrator;
        private readonly int _sdkVersion;
        private bool _disabled;

        public AndroidHapticsService()
        {
            using AndroidJavaClass version = new("android.os.Build$VERSION");
            _sdkVersion = version.GetStatic<int>("SDK_INT");
            using AndroidJavaClass player = new("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }

        public void Light() => Vibrate(2, 8L, 55);
        public void Soft() => Vibrate(0, 12L, 90);
        public void Heavy() => Vibrate(5, 24L, 190);

        private void Vibrate(int predefinedEffect, long fallbackDuration, int fallbackAmplitude)
        {
            if (_disabled || _vibrator == null) return;

            try
            {
                using AndroidJavaClass effectClass = new("android.os.VibrationEffect");
                if (_sdkVersion >= 29)
                {
                    using AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createPredefined", predefinedEffect);
                    _vibrator.Call("vibrate", effect);
                }
                else if (_sdkVersion >= 26)
                {
                    using AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", fallbackDuration, fallbackAmplitude);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", fallbackDuration);
                }
            }
            catch (Exception exception)
            {
                _disabled = true;
                Debug.LogWarning($"Haptics unavailable and disabled for this session: {exception.Message}");
            }
        }
    }
#endif
}
