# A Gallery of Interactive Fluid Simulations

**A project for University of Tartu Computer Graphics Course (2025).** 
#### Creators
**Henrik Peegel ([TheShadowfun](https://github.com/TheShadowfun)), Jaakob-Jaan Avvo ([Jawa4a](https://github.com/Jawa4a)), Ander Pavlov ([vapsik](https://github.com/vapsik))**
### Introduction
As of November 8, 2025, this project is in early progress. The current rendition of the README introduces the goals of the project as well as the intended assignment distribution between the creators.

The goal of this project is to create a gallery of interactive 2D fluid simulations that enact the [Navier-Stokes equations for incompressible fluids](https://en.wikipedia.org/wiki/Navier%E2%80%93Stokes_equations). The algorithm used to simulate the fluids is *Stable Fluids* based on the Jos Stam's seminal work, [a 2001 article](https://pages.cs.wisc.edu/~chaol/data/cs777/stam-stable_fluids.pdf) in the field of real-time fluid simulation that has been extensively used in video games since its inception. In this algorithm, a simple incompressible (constant density) fluid is represented in a rectangular grid as a field coupled velocity vectors and pressure scalars that will dictate the movement of the fluid. The method relies on implicit diffusion, semi-Lagrangian advection and Gauss-Seidel projection, which all allow for stable, large time-step simulation without numerical divergence, combined with a projection step that enforces incompressibility, which makes the velocity field divergence-free using the [Helmholtz decomposition theorem](https://en.wikipedia.org/wiki/Helmholtz_decomposition). Diffusion, external forces, and boundary interactions are incorporated in a computationally efficient time evolution scheme such that larger time steps can be used without losing physical accuracy or stability.

The first proof-of-concept non-interactive prototypes of the simulations will be created using Python in a Jupyter notebook. These prototype notebooks will establish the groundwork for visually enhanced and interactive simulations in the Unity game engine, with computations parallelized via **compute shaders** for real-time performance.

The spatial scope of this project is initially a **2D rectangular domain** (with added objects as additional boundaries inside the domain) since simulating fluids in 2D is less complex, less computationally demanding and more controllable, although the _Stable Fluids_ algorithm can be implemented in 3D as well.
### Goals for Simulation
According to the current plans the gallery will include the following interactive 2D fluid simulations:
- [ ] MVP made with Unity should include the following:
	- A fluid source.
	- A way to interact with fluid, either:
		- a velocity brush such as in Sebastian Lague's video;
		- a moving boundary, which applies force on the surrounding while being moved.
	- Either or both solid **boundary conditions** and/or **periodic boundary** conditions.
		- For periodic boundary conditions, **FFT** could be applied on the to fluid to solve it in frequency domain.
- [ ] A set of Different Interactive Fluid/Smoke Simulation:
	* There will be a set of simulations with preset initialization parameters. 
	* To simulate a fluid as a smoke a non-zero net **buoyancy force** will be set to grids with smoke such that it will behave as if it's lighter than the surrounding medium
	* **Interactivity** will be either in the form of changing the position of the fluid source or in the form of moving the objects that will act as **boundary** and its movement will apply additional forces on the surrounding fluid.
	* User-induced motion will probably be implemented using **[Verlet integrator](https://en.wikipedia.org/wiki/Verlet_integration)** or some other kind of **symplectic integrator** since the common Euler or Runge-Kutta ODE solvers lack the ability to preserve energy over long simulations.
- [ ] An interactive Sandbox:
	* User will be given a chance to change the parameters before initializing the simulation.
- [ ] Plasma simulation:
	* The simulation will be constructed according to a simplified idea of [*Magnetohydrodynamics (MHD)*](https://en.wikipedia.org/wiki/Magnetohydrodynamics) which simulates a plasma as an incompressible fluid with moving charges that make fluid susceptible to the [Lorentz force](https://en.wikipedia.org/wiki/Lorentz_force).
	* More research and perhaps even original code will be required for this goal since there isn't a lot of material on this kind of particular simulation, therefore it is not guaranteed that this goal will be completed by the final deadline. It's possible that some other implementation scheme such as [Particle in-a-cell](https://en.wikipedia.org/wiki/Particle-in-cell) can suit this task better. 

### Distribution of Tasks (as of November 8, 2025)
**Ander Pavlov** **([vapsik](https://github.com/vapsik))** - (Responsible for physics and algorithms) Will create initial low-res non-interactive proof-of-concept fluid simulation prototypes in Python with implementation steps and pseudocode in a well-documented Jupyter notebook. Will also create the list of resources as well as math and implementation notes in the repository that the simulation will be based on. Helps with the compute shader implementation.

**Henrik Peegel ([TheShadowfun](https://github.com/TheShadowfun))** - (Responsible for implementation) Will implement the fluid simulation algorithms in Unity and scale them with the help of *Unity compute shaders*.

**Jaakob-Jaan Avvo ([Jawa4a](https://github.com/Jawa4a))** - (Responsible for implementation) Helps with the compute shader implementation and is responsible for user-input-caused interaction and simulation stylization (textures).