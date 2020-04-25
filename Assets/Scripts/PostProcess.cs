using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class PostProcess : MonoBehaviour
{
	public Shader fireShader;
	Material material; // Material for fireShader
	public Transform container; // Container for our fire

	// Noise
	public int size = 256; // As of now, needs to be a mutliple of 8
	public float scale = 50; // TODO: Use this value
	public ComputeShader noiseShader;
	RenderTexture renderTexture;

	// If the specified texture does not exist, create it
    // Source: https://github.com/SebLague/Clouds
    void createTexture (ref RenderTexture texture, int resolution) {
        if (texture == null || !texture.IsCreated () || texture.width != resolution || texture.height != resolution || texture.volumeDepth != resolution) {
            if (texture != null) {
                texture.Release ();
            }
            texture = new RenderTexture (resolution, resolution, 0, RenderTextureFormat.RFloat);
            texture.volumeDepth = resolution;
            texture.enableRandomWrite = true;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Create ();
        }
    }

    void createMaterial(ref Material material, ref Shader shader) {
        if (material == null) {
            material = new Material(shader);
        }
    }

	private void updateNoise() {
        // If texture does not exist, create it
		createTexture(ref renderTexture, size);
        // Find kernel
		int kernelHandle = noiseShader.FindKernel("SimpleNoise");
        // Input
		noiseShader.SetTexture(kernelHandle, "Result", renderTexture);
        // Calculate
		noiseShader.Dispatch(kernelHandle, size / 8, size / 8, size / 8);
        // Output
		material.SetTexture("_Noise", renderTexture);
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        createMaterial(ref material, ref fireShader);
		// Set container bounds
		material.SetVector("boundsMin", container.position - container.localScale / 2);
		material.SetVector("boundsMax", container.position + container.localScale / 2);
		// Generate noise
		updateNoise();
		// Render
		Graphics.Blit(source, destination, material);
	}
}
