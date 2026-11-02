using UnityEngine;

// Renders only staged copies. The two backgrounds recover correct transparency
// even for the project's shaders that blend their alpha into the render target.
public static class PrefabPreviewCapture
{
    const int ImageSize = 512;
    const float FramingPadding = 1.1f;
    const float MinimumPreviewWorldSize = 4f;

    public static byte[] Capture(GameObject staging)
    {
        int previewLayer = LayerMask.NameToLayer("PrefabPreview");
        if (staging == null || previewLayer < 0)
        {
            Debug.LogError("Prefab preview requires a staging object and the PrefabPreview layer.");
            return null;
        }

        GameObject cameraObject = null;
        Camera camera = null;
        RenderTexture target = null;
        Texture2D blackImage = null;
        Texture2D whiteImage = null;
        Texture2D result = null;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            // The copies are inactive while serialized. Strip scripts and physics
            // before activating them so capturing cannot affect the live level.
            foreach (MonoBehaviour component in staging.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // World-space TextMeshPro is itself a rendering component.
                if (!(component is TMPro.TMP_Text))
                    Object.DestroyImmediate(component);
            }
            foreach (Collider2D component in staging.GetComponentsInChildren<Collider2D>(true))
                Object.DestroyImmediate(component);
            foreach (Rigidbody2D component in staging.GetComponentsInChildren<Rigidbody2D>(true))
                Object.DestroyImmediate(component);
            foreach (Collider component in staging.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(component);
            foreach (Rigidbody component in staging.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(component);
            foreach (AudioSource component in staging.GetComponentsInChildren<AudioSource>(true))
                Object.DestroyImmediate(component);
            foreach (Camera component in staging.GetComponentsInChildren<Camera>(true))
                Object.DestroyImmediate(component);
            foreach (Transform child in staging.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = previewLayer;

            staging.SetActive(true);
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer renderer in staging.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds || bounds.size.x <= 0f || bounds.size.y <= 0f)
            {
                Debug.LogError("Prefab preview could not be captured because the selected objects have no visible bounds.");
                return null;
            }

            cameraObject = new GameObject("Prefab Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            float frameSize = Mathf.Max(MinimumPreviewWorldSize,
                Mathf.Max(bounds.size.x, bounds.size.y) * FramingPadding);
            camera.orthographicSize = frameSize * 0.5f;
            camera.aspect = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = 1 << previewLayer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10f);

            target = RenderTexture.GetTemporary(ImageSize, ImageSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            camera.targetTexture = target;
            blackImage = RenderAgainst(camera, target, Color.black);
            whiteImage = RenderAgainst(camera, target, Color.white);
            Color32[] black = blackImage.GetPixels32();
            Color32[] white = whiteImage.GetPixels32();
            Color32[] pixels = new Color32[black.Length];
            bool linearColor = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = RecoverPixel(black[i], white[i], linearColor);

            result = new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false);
            result.SetPixels32(pixels);
            result.Apply();
            return result.EncodeToPNG();
        }
        finally
        {
            staging.SetActive(false);
            RenderTexture.active = previousActive;
            if (camera != null) camera.targetTexture = null;
            if (cameraObject != null) DestroyTemporary(cameraObject);
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (blackImage != null) DestroyTemporary(blackImage);
            if (whiteImage != null) DestroyTemporary(whiteImage);
            if (result != null) DestroyTemporary(result);
        }
    }

    static void DestroyTemporary(Object temporary)
    {
        if (Application.isPlaying)
            Object.Destroy(temporary);
        else
            Object.DestroyImmediate(temporary);
    }

    static Color32 RecoverPixel(Color32 black, Color32 white, bool linearColor)
    {
        float alpha = Mathf.Clamp01(1f - ((white.r - black.r) +
            (white.g - black.g) + (white.b - black.b)) / (3f * 255f));
        if (alpha <= 1f / 255f)
            return new Color32(0, 0, 0, 0);

        float r = Mathf.Clamp01(black.r / (255f * alpha));
        float g = Mathf.Clamp01(black.g / (255f * alpha));
        float b = Mathf.Clamp01(black.b / (255f * alpha));
        if (linearColor)
        {
            r = Mathf.LinearToGammaSpace(r);
            g = Mathf.LinearToGammaSpace(g);
            b = Mathf.LinearToGammaSpace(b);
        }
        return new Color(r, g, b, alpha);
    }

    static Texture2D RenderAgainst(Camera camera, RenderTexture target, Color background)
    {
        camera.backgroundColor = background;
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(ImageSize, ImageSize, TextureFormat.RGB24, false, true);
        image.ReadPixels(new Rect(0f, 0f, ImageSize, ImageSize), 0, 0);
        image.Apply();
        return image;
    }
}
