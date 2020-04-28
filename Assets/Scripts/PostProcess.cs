using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class PostProcess : MonoBehaviour
{
	public Shader fireShader; // Input fire voxels, output a render of the fire
	Material material; // Material for fireShader
	public Transform container; // Container for our fire

	// Noise
	public int size = 256; // As of now, needs to be a mutliple of 8
	public float scale = 50; // TODO: Use this value
    public float step_size = 0.00001f; // TODO: Use this value
	public ComputeShader noiseShader;

    public ComputeShader fireComputeShader;
	RenderTexture renderTexture;
	RenderTexture velocityTex;
	RenderTexture velocityTexRes;
	RenderTexture pressureTex;
	RenderTexture pressureTexRes;
	RenderTexture divergenceTex;
	RenderTexture temperatureTex;
	RenderTexture temperatureTexRes;
	RenderTexture densityTex;
	RenderTexture densityTexRes;
	RenderTexture debugTex;

    // Ray marching
    public int marchSteps = 4;

	// If the specified texture does not exist, create it
    // Source: https://github.com/SebLague/Clouds
    void createTexture (ref RenderTexture texture, int resolution, int num_channels) {
        if (texture == null || !texture.IsCreated () || texture.width != resolution || texture.height != resolution || texture.volumeDepth != resolution) {
            if (texture != null) {
                texture.Release ();
            }
			if (num_channels == 4) texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
			else if (num_channels == 1) texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat);
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
		createTexture(ref renderTexture, size, 4);
        // Find kernel
		int kernelHandle = noiseShader.FindKernel("SimpleNoise");
        // Input
		noiseShader.SetTexture(kernelHandle, "Result", renderTexture);
        // Calculate
		noiseShader.Dispatch(kernelHandle, size / 8, size / 8, size / 8);
        // Output
		material.SetTexture("Noise", renderTexture);

	}

    private void updateFire() {
		// If texture does not exist, create it
		createTexture(ref velocityTex, size, 4);
		createTexture(ref velocityTexRes, size, 4);
		createTexture(ref pressureTex, size, 1);
		createTexture(ref pressureTexRes, size, 1);
		createTexture(ref densityTex, size, 1);
		createTexture(ref densityTexRes, size, 1);
		createTexture(ref temperatureTex, size, 1);
		createTexture(ref temperatureTexRes, size, 1);
		createTexture(ref divergenceTex, size, 1);
		createTexture(ref debugTex, size, 1);
		// Switch res
		RenderTexture temp, temp0, temp1;
		temp = velocityTex;
		velocityTex = velocityTexRes;
		velocityTexRes = temp;

		// Find kernel
		int pressureHandle = fireComputeShader.FindKernel("InitPressure");
		int initHandle = fireComputeShader.FindKernel("InitFire");
		int advectionHandle = fireComputeShader.FindKernel("Advection");
		int divergenceHandle = fireComputeShader.FindKernel("Divergence");
		int jacobiHandle = fireComputeShader.FindKernel("Jacobi");
		int projectionHandle = fireComputeShader.FindKernel("Projection");
        int buoyancyHandle = fireComputeShader.FindKernel("ApplyBuoyancy");
		// static Input
		foreach (int handle in new int[] { pressureHandle, advectionHandle, divergenceHandle, jacobiHandle, projectionHandle })
		{
			fireComputeShader.SetTexture(handle, "divergenceTex", divergenceTex);
		}
		fireComputeShader.SetInt("size", size);
		// Calculate Advection
		fireComputeShader.SetTexture(advectionHandle, "velocityTex", velocityTex);
		fireComputeShader.SetTexture(advectionHandle, "debugTex", debugTex);
		fireComputeShader.SetTexture(advectionHandle, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(advectionHandle, "temperatureTex", temperatureTex);
		fireComputeShader.SetTexture(advectionHandle, "temperatureTexRes", temperatureTexRes);
		fireComputeShader.SetTexture(advectionHandle, "densityTex", densityTex);
		fireComputeShader.SetTexture(advectionHandle, "densityTexRes", densityTexRes);
		fireComputeShader.Dispatch(advectionHandle, size / 8, size / 8, size / 8);
		GL.Flush();


		fireComputeShader.SetTexture(initHandle, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(initHandle, "densityTexRes", densityTexRes);
		fireComputeShader.SetTexture(initHandle, "temperatureTexRes", temperatureTexRes);
		fireComputeShader.Dispatch(initHandle, size / 8, size / 8, size / 8);
		GL.Flush();

		// Calculate Buoyancy
		fireComputeShader.SetTexture(buoyancyHandle, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(buoyancyHandle, "temperatureTex", temperatureTexRes); // Hot Read
		fireComputeShader.SetTexture(buoyancyHandle, "densityTex", densityTexRes); // Hot Read
	    fireComputeShader.Dispatch(buoyancyHandle, size / 8, size / 8, size / 8);
		GL.Flush();

		// Switch res
		temp = velocityTex;
		velocityTex = velocityTexRes;
		velocityTexRes = temp;


		// Calculate divergence, jacobi, projection
		fireComputeShader.SetTexture(divergenceHandle, "velocityTex", velocityTex);
		fireComputeShader.SetTexture(divergenceHandle, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(jacobiHandle, "velocityTex", velocityTex);
		fireComputeShader.SetTexture(jacobiHandle, "velocityTexRes", velocityTexRes);

		fireComputeShader.Dispatch(divergenceHandle, size / 8, size / 8, size / 8);
		GL.Flush();
		for (int itr = 0; itr < 20; itr++)
		{
			fireComputeShader.SetTexture(jacobiHandle, "pressureTex", pressureTex);
			fireComputeShader.SetTexture(jacobiHandle, "pressureTexRes", pressureTexRes);
			fireComputeShader.Dispatch(jacobiHandle, size / 8, size / 8, size / 8);
			GL.Flush();
			// Switch res
			temp = pressureTex;
			pressureTex = pressureTexRes;
			pressureTexRes = temp;
		}

		fireComputeShader.SetTexture(pressureHandle, "densityTexRes", densityTexRes);
		fireComputeShader.SetTexture(pressureHandle, "temperatureTexRes", temperatureTexRes);
		fireComputeShader.SetTexture(pressureHandle, "pressureTexRes", pressureTexRes);

		//fireComputeShader.Dispatch(pressureHandle, size / 8, size / 8, size / 8);
		//GL.Flush();

		// TODO: write a swap helper function
		//temp = pressureTex;
		//pressureTex = pressureTexRes;
		//pressureTexRes = temp;

		temp0 = temperatureTex;
		temperatureTex = temperatureTexRes;
		temperatureTexRes = temp0;
		temp1 = densityTex;
		densityTex = densityTexRes;
		densityTexRes = temp1;


		fireComputeShader.SetTexture(projectionHandle, "velocityTex", velocityTex);
		fireComputeShader.SetTexture(projectionHandle, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(projectionHandle, "pressureTex", pressureTex);
		fireComputeShader.Dispatch(projectionHandle, size / 8, size / 8, size / 8);
		GL.Flush();
		// Output
		material.SetTexture("Velocity", velocityTexRes);
		material.SetTexture("Density", densityTex);
		material.SetTexture("Debug", debugTex);
	}

	private void restartFire() {
		velocityTex.Release();
		velocityTexRes.Release();
		densityTex.Release();
		densityTexRes.Release();
		temperatureTex.Release();
		temperatureTexRes.Release();
		pressureTex.Release();
		divergenceTex.Release();

	}

	// Source: framebuffer after unity's pipeline
	private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        createMaterial(ref material, ref fireShader);
        // Pass variables to shader
		material.SetInt ("marchSteps", marchSteps);
		material.SetVector("boundsMin", container.position - container.localScale / 2);
		material.SetVector("boundsMax", container.position + container.localScale / 2);
		updateFire();
		// Render
		Graphics.Blit(source, destination, material);
	}
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            print("Updating fire");
			updateFire();
        } else if (Input.GetKeyDown(KeyCode.R)) {
			print("Clearing fire");
			restartFire();
		}
    }

}
