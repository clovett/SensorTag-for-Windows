using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BigRedButton
{
    class AudioManager
    {
        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            void _VtblGap1_1();
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }
        private static class MMDeviceEnumeratorFactory
        {
            public static IMMDeviceEnumerator CreateInstance()
            {
                return (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))); // a MMDeviceEnumerator
            }
        }
        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolume
        {
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE RegisterControlChangeNotify(/* [in] */__in IAudioEndpointVolumeCallback *pNotify) = 0;
            int RegisterControlChangeNotify(IntPtr pNotify);
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE UnregisterControlChangeNotify(/* [in] */ __in IAudioEndpointVolumeCallback *pNotify) = 0;
            int UnregisterControlChangeNotify(IntPtr pNotify);
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetChannelCount(/* [out] */ __out UINT *pnChannelCount) = 0;
            int GetChannelCount(ref uint pnChannelCount);
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE SetMasterVolumeLevel( /* [in] */ __in float fLevelDB,/* [unique][in] */ LPCGUID pguidEventContext) = 0;
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE SetMasterVolumeLevelScalar( /* [in] */ __in float fLevel,/* [unique][in] */ LPCGUID pguidEventContext) = 0;
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetMasterVolumeLevel(/* [out] */ __out float *pfLevelDB) = 0;
            int GetMasterVolumeLevel(ref float pfLevelDB);
            //virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetMasterVolumeLevelScalar( /* [out] */ __out float *pfLevel) = 0;
            int GetMasterVolumeLevelScalar(ref float pfLevel);

            // virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE SetChannelVolumeLevel(/* [annotation][in] */ _In_ UINT nChannel, float fLevelDB, /* [unique][in] */ LPCGUID pguidEventContext) = 0;
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);

            // virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE SetChannelVolumeLevelScalar( /* [annotation][in] */ _In_ UINT nChannel, float fLevel, /* [unique][in] */ LPCGUID pguidEventContext) = 0;
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevelDB, Guid pguidEventContext);

            // virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetChannelVolumeLevel( /* [annotation][in] */ _In_ UINT nChannel, /* [annotation][out] */ _Out_  float* pfLevelDB) = 0;
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);

            // virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetChannelVolumeLevelScalar( /* [annotation][in] */ _In_ UINT nChannel, /* [annotation][out] */ _Out_  float* pfLevel) = 0;
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevelDB);

            // virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE SetMute( /* [annotation][in] */ _In_ BOOL bMute, /* [unique][in] */ LPCGUID pguidEventContext) = 0;
            int SetMute(int bMute, ref Guid pguidEventContext);

            // virtual /* [helpstring] */ HRESULT STDMETHODCALLTYPE GetMute( /* [annotation][out] */ _Out_ BOOL *pbMute) = 0;
            int GetMute(out int pbMute);
        }

        public static bool IsMute()
        {
            bool mute = false;
            try
            {
                IMMDeviceEnumerator deviceEnumerator = MMDeviceEnumeratorFactory.CreateInstance();
                IMMDevice speakers;
                const int eRender = 0;
                const int eMultimedia = 1;
                deviceEnumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out speakers);

                object aepv_obj;
                speakers.Activate(typeof(IAudioEndpointVolume).GUID, 0, IntPtr.Zero, out aepv_obj);
                IAudioEndpointVolume aepv = (IAudioEndpointVolume)aepv_obj;
                int v = 0;
                int res = aepv.GetMute(out v);
                mute = (v == 1);

                Console.WriteLine($"Audio mute level is {mute}");
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"**Could not read system mute level** {ex.Message}"); 
            }
            return mute;
        }

        public static int SetMute(bool mute)
        {
            try
            {
                IMMDeviceEnumerator deviceEnumerator = MMDeviceEnumeratorFactory.CreateInstance();
                IMMDevice speakers;
                const int eRender = 0;
                const int eMultimedia = 1;
                deviceEnumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out speakers);

                object aepv_obj;
                speakers.Activate(typeof(IAudioEndpointVolume).GUID, 0, IntPtr.Zero, out aepv_obj);
                IAudioEndpointVolume aepv = (IAudioEndpointVolume)aepv_obj;
                Guid ZeroGuid = new Guid();
                int v = mute ? 1 : 0;
                int res = aepv.SetMute(v, ref ZeroGuid);

                Console.WriteLine($"Audio mute is set to {mute}");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"**Could not set system mute level** {ex.Message}");
                return ex.HResult;
            }
        }

        public static void SetMasterVolume(float volume)
        {
            if (volume < 0 || volume > 1)
            {
                throw new ArgumentOutOfRangeException("Provide volumet between 0 and 1");
            }
            try
            {
                IMMDeviceEnumerator deviceEnumerator = MMDeviceEnumeratorFactory.CreateInstance();
                IMMDevice speakers;
                const int eRender = 0;
                const int eMultimedia = 1;
                deviceEnumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out speakers);

                object aepv_obj;
                speakers.Activate(typeof(IAudioEndpointVolume).GUID, 0, IntPtr.Zero, out aepv_obj);
                IAudioEndpointVolume aepv = (IAudioEndpointVolume)aepv_obj;
                Guid ZeroGuid = new Guid();
                int res = aepv.SetMasterVolumeLevelScalar(volume, ZeroGuid);

                Console.WriteLine($"Audio volume set to {volume}%");
            }
            catch (Exception ex) { Console.WriteLine($"**Could not set audio level** {ex.Message}"); }
        }
    }
}
