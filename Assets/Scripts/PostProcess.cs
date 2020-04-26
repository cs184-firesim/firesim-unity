using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class PostProcess : MonoBehaviour
{
	public Shader fireShader; // Input fire voxels, output a render of the fire
	Material material; // Material for fireShader
	public Transform container; // Container for our fire

	// Noise
	public int size = 256; // As of now, needs to be a mutliple of 8
	public float scale = 50; // TODO: Use this value
    public float step_size = .001f; // TODO: Use this value
	public ComputeShader noiseShader;

    public ComputeShader fireComputeShader;
	//RenderTexture renderTexture;
	RenderTexture velocityTex;
    RenderTexture pressureTex;
    RenderTexture divergenceTex;

	// If the specified texture does not exist, create it
    // Source: https://github.com/SebLague/Clouds
    void createTexture (ref RenderTexture texture, int resolution) {
        if (texture == null || !texture.IsCreated () || texture.width != resolution || texture.height != resolution || texture.volumeDepth != resolution) {
            if (texture != null) {
                texture.Release ();
            }
            texture = new RenderTexture (resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
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
		// TODO: Call the right kernels and store to the right buffers
		// Example:

		//// If texture does not exist, create it
		//createTexture(ref renderTexture, size);
		//// Find kernel
		//int kernelHandle = noiseShader.FindKernel("SimpleNoise");
		//// Input
		//noiseShader.SetTexture(kernelHandle, "Result", renderTexture);
		//// Calculate
		//noiseShader.Dispatch(kernelHandle, size / 8, size / 8, size / 8);
		//// Output
		//material.SetTexture("_Noise", renderTexture);
	}

    private void updateFire() {
		// If texture does not exist, create it
		createTexture(ref velocityTex, size);
        createTexture(ref pressureTex, size);
        createTexture(ref divergenceTex, size);
        // Find kernel TODO: figure out where fireShader is initialized
        int initHandle = fireComputeShader.FindKernel("InitFire");
        int advectionHandle = fireComputeShader.FindKernel("Advection");
		int divergenceHandle = fireComputeShader.FindKernel("Divergence");
		int jacobiHandle = fireComputeShader.FindKernel("Jacobi");
		int projectionHandle = fireComputeShader.FindKernel("Projection");
		// Input
		fireComputeShader.SetTexture(initHandle, "velocityTex", velocityTex);
		fireComputeShader.SetTexture(initHandle, "pressureTex", pressureTex);
		fireComputeShader.SetTexture(initHandle, "divergenceTex", divergenceTex);
		// Calculate
		fireComputeShader.Dispatch(initHandle, size / 8, size / 8, size / 8);
		fireComputeShader.Dispatch(advectionHandle, size / 8, size / 8, size / 8);
		fireComputeShader.Dispatch(divergenceHandle, size / 8, size / 8, size / 8);
		for (int itr = 0; itr < 20; itr++) {
			fireComputeShader.Dispatch(jacobiHandle, size / 8, size / 8, size / 8);
		}
		fireComputeShader.Dispatch(projectionHandle, size / 8, size / 8, size / 8);
		// Output
		material.SetTexture("_Velocity", velocityTex);
	}

	// Source: framebuffer after unity's pipeline
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
