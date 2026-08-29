using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Keeps the desktop post-processing profile intact while replacing the
/// compute-shader based ambient occlusion method on WebGL at runtime.
/// </summary>
public static class WebGLPostProcessingCompatibility
{
#if UNITY_WEBGL
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void UseWebGLCompatibleAmbientOcclusion()
    {
        PostProcessVolume[] volumes = Object.FindObjectsOfType<PostProcessVolume>(true);

        for (int i = 0; i < volumes.Length; i++)
        {
            PostProcessVolume volume = volumes[i];
            if (volume == null || volume.sharedProfile == null)
            {
                continue;
            }

            // Accessing profile creates a runtime-only copy. The shared asset and
            // the Windows configuration therefore remain unchanged.
            PostProcessProfile runtimeProfile = volume.profile;
            if (runtimeProfile == null
                || !runtimeProfile.TryGetSettings(out AmbientOcclusion ambientOcclusion)
                || ambientOcclusion == null
                || !ambientOcclusion.enabled.value)
            {
                continue;
            }

            ambientOcclusion.mode.overrideState = true;
            ambientOcclusion.mode.value = AmbientOcclusionMode.ScalableAmbientObscurance;
        }
    }
#endif
}
