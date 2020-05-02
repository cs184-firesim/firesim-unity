﻿using System.Collections;
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
	public int stepc = 0;
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

	private void updateEverything() {

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

        

		int handle = fireComputeShader.FindKernel("Everything");
		if (stepc % 2 == 0)
		{ 
			fireComputeShader.SetTexture(handle, "velocityTex", velocityTex);
			fireComputeShader.SetTexture(handle, "velocityTexRes", velocityTexRes);
			fireComputeShader.SetTexture(handle, "temperatureTex", temperatureTex);
			fireComputeShader.SetTexture(handle, "temperatureTexRes", temperatureTexRes);
			fireComputeShader.SetTexture(handle, "densityTex", densityTex);
			fireComputeShader.SetTexture(handle, "densityTexRes", densityTexRes);
			fireComputeShader.SetTexture(handle, "pressureTex", pressureTex);
			fireComputeShader.SetTexture(handle, "pressureTexRes", pressureTexRes);
		}
		else {
			fireComputeShader.SetTexture(handle, "velocityTexRes", velocityTex);
			fireComputeShader.SetTexture(handle, "velocityTex", velocityTexRes);
			fireComputeShader.SetTexture(handle, "temperatureTexRes", temperatureTex);
			fireComputeShader.SetTexture(handle, "temperatureTex", temperatureTexRes);
			fireComputeShader.SetTexture(handle, "densityTexRes", densityTex);
			fireComputeShader.SetTexture(handle, "densityTex", densityTexRes);
			fireComputeShader.SetTexture(handle, "pressureTexRes", pressureTex);
			fireComputeShader.SetTexture(handle, "pressureTex", pressureTexRes);
		}
		
		fireComputeShader.SetTexture(handle, "debugTex", debugTex);
		fireComputeShader.SetFloat("timeStep", 0.8f);
		fireComputeShader.SetInt("size", size);



		fireComputeShader.Dispatch(handle, size / 8, size / 8, size / 8);

		stepc += 1;
		if (stepc % 2 == 0)
		{
			material.SetTexture("Velocity", velocityTexRes);
			material.SetTexture("Density", densityTexRes);
		}
        else
		{
			material.SetTexture("Velocity", velocityTex);
			material.SetTexture("Density", densityTex);
		}
		material.SetTexture("Debug", debugTex);
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
		int initHandle = fireComputeShader.FindKernel("InitFire");
		int advectionHandle = fireComputeShader.FindKernel("Advection");
		int divergenceHandle = fireComputeShader.FindKernel("Divergence");
		int jacobiHandle = fireComputeShader.FindKernel("Jacobi");
		int projectionHandle = fireComputeShader.FindKernel("Projection");
        int buoyancyHandle = fireComputeShader.FindKernel("ApplyBuoyancy");
		// static Input
		foreach (int handle in new int[] {advectionHandle, divergenceHandle, jacobiHandle, projectionHandle })
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
		fireComputeShader.SetFloat("timeStep", 0.8f);
		fireComputeShader.Dispatch(advectionHandle, size / 8, size / 8, size / 8);
		GL.Flush();


        // Initialization
		fireComputeShader.SetTexture(initHandle, "debugTex", debugTex);
		
		fireComputeShader.SetTexture(initHandle, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(initHandle, "densityTexRes", densityTexRes);
		fireComputeShader.SetTexture(initHandle, "temperatureTexRes", temperatureTexRes);
		fireComputeShader.Dispatch(initHandle, size / 8, size / 8, size / 8);
		

		// Calculate Buoyancy
		fireComputeShader.SetTexture(buoyancyHandle, "debugTex", debugTex);
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
		fireComputeShader.SetTexture(divergenceHandle, "debugTex", debugTex);
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
		int clearKernel1 = fireComputeShader.FindKernel("Clear");
		int clearKernel2 = fireComputeShader.FindKernel("Clear2");
		fireComputeShader.SetTexture(clearKernel1, "velocityTex", velocityTex);
		fireComputeShader.SetTexture(clearKernel1, "velocityTexRes", velocityTexRes);
		fireComputeShader.SetTexture(clearKernel1, "temperatureTex", temperatureTex);
		fireComputeShader.SetTexture(clearKernel1, "temperatureTexRes", temperatureTexRes);
		fireComputeShader.SetTexture(clearKernel1, "densityTex", densityTex);
		fireComputeShader.SetTexture(clearKernel1, "densityTexRes", densityTexRes);
		fireComputeShader.SetTexture(clearKernel1, "pressureTex", pressureTex);
		fireComputeShader.SetTexture(clearKernel1, "pressureTexRes", pressureTexRes);
		fireComputeShader.SetTexture(clearKernel2, "divergenceTex", divergenceTex);
		fireComputeShader.Dispatch(clearKernel1, size / 8, size / 8, size / 8);
		fireComputeShader.Dispatch(clearKernel2, size / 8, size / 8, size / 8);
	}

	// Source: framebuffer after unity's pipeline
	private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        createMaterial(ref material, ref fireShader);
        // Pass variables to shader
		material.SetInt ("marchSteps", marchSteps);
		material.SetVector("boundsMin", container.position - container.localScale / 2);
		material.SetVector("boundsMax", container.position + container.localScale / 2);
		//updateEverything();
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
			//updateEverything();
		} else if (Input.GetKeyDown(KeyCode.R)) {
			print("Clearing fire");
			restartFire();
		}
    }

}
