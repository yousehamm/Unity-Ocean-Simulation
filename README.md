# IFFT Ocean Simulation in Unity
A real-time ocean rendering and simulation framework built in Unity, using the JONSWAP-TMA spectrum.
<img width="800" height="450" alt="Movie_Main_Water" src="https://github.com/user-attachments/assets/5d52ae60-c672-4376-9a15-f91d78a9f907" />

# Current Features/Progress:
* Full Ocean Displacement & Normal Calculations
<img width="800" height="450" alt="Movie_Displacement_Water" src="https://github.com/user-attachments/assets/09642405-edaa-4fc3-b584-c565a1aac3af" />
  * Initially used the Standard Phillips Ocean Spectrum, before switching to JONSWAP + TMA
  * Conversion from Spectra to Displacement done via the Inverse Fast Fourier Transform (Cooley-Tukey Algorithm)
  * Both IFFT & Spectra computed in HLSL on the GPU in Parallel
  * Capable of Multiple Cascades of various LODs

* Initial Rigid-Body Physics & Buoyancy Integration
<img width="800" height="450" alt="Movie_Displacement_Water" src="https://github.com/user-attachments/assets/f8446846-f5c9-4236-aed9-d70faf9e94c4" />
  * Asynchronous GPU readback (AsyncGPUReadback), allowing the transfer of wave displacements to the CPU without stalling the rendering thread
  * Dynamic Buoyancy Force and Torque calculations based on Physics Points along a mesh

* Ocean Surface Effects
<img width="800" height="450" alt="image" src="https://github.com/user-attachments/assets/bc6c4ddc-b1ec-4212-b7e7-3d978f72aab6" />
  * Specular Reflections for realistic Sun Glints
  * Environmental Reflections based on Dynamic Skybox
  * Subsurface Scattering on thin Wave Crests
  * Underwater Refraction & Depth-Based Color Absorption

* Dynamic Skybox
<img width="800" height="450" alt="ezgif com-optimize (1)" src="https://github.com/user-attachments/assets/276d617d-33fb-4916-9a0c-923706860cc9" />
  * Physically Based with procedural atmospheric scattering model
  * Synced with Sun/Main Light Position

# In Progress:
* Dynamic Waterline Mask to Displace Surface and Underwater effects Simultaneously
* Dynamic Underwater Caustics
* Underwater Volumetrics
* Jacobian Foam

# Planed Features:
* Better Foam & Spray
* Bubbles & Other Underwater Effects
* Coastal Effects/Wave Breaking
* Improved Buoyancy Physics
* Improved/Realtime Reflections

# Performance
Evaluated on an **NVIDIA GeForce RTX 5070 Ti Laptop GPU** at **1440p resolution**:
* **Global Frame Time:** ~12 ms
* **Average GPU Frame Time:**  ~10 ms 
* **Active Dynamic Vertices:**  ~8,950,800 vertices simulated in parallel 

# Requirements & Setup
* Engine: Unity 6000.5.7f1
* Pipeline: URP
* To Run: Clone the repository and open the "Main_Ocean_Scene" in the MyAssets -> Scenes folder.

## Acknowledgements & References

* **Acerola**: Major Inspiration for architectural design, graphics breakdowns, and advanced HLSL rendering pipelines. ([YouTube Channel](https://www.youtube.com/@Acerola_t))
* **JONSWAP Spectrum**: Hasselmann et al. (1973), *"Measurements of Wind-Wave Growth and Swell Decay during the Joint North Sea Wave Project"*
* **Ocean Simulation Foundations**: Jerry Tessendorf (2001), *"Simulating Ocean Water"*, SIGGRAPH Course Notes.

## License

Distributed under the **MIT License**. See the `LICENSE` file for more details.
