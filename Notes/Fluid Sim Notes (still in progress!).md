These notes are based on the following resources:
* [Fluid Simulation Course Notes, University of British Columbia](https://www.cs.ubc.ca/~rbridson/fluidsimulation/fluids_notes.pdf)
* [Stable Fluids by Jos Stam](https://pages.cs.wisc.edu/~chaol/data/cs777/stam-stable_fluids.pdf)
* [Fluid Flow Tutorial by Karl Sims](https://www.karlsims.com/fluid-flow.html)
* [Coding Adventure: Simulating Smoke by Sebastian Lague](https://www.youtube.com/watch?v=Q78wvrQ9xsU)
## The Theory and Implementation
Implementation notes are written in *C#*.
### Navier Stokes Equations for Incompressible fluids
Stable fluids assumes that the simulated fluid is uniformly viscous and non-compressible e.g its density is constant across the spatial domain and time. Although real fluids are not incompressible, it is still a good approximation for simulating many real fluids such that water or gases which usually have low compressibility factor when the conditions such as pressure or temperature are not fluctuating too much.

Navier-Stokes equations are partial differential equations that are for this kind of fluid stated as
$$\begin{align}
\tag{1}&\frac{\partial\vec{u}}{\partial t} + (\vec{u}\cdot \nabla)\vec{u}=  \frac{-\nabla p}{\rho} + \frac{\vec{F}_{\text{external}}}{\rho} + \nu \nabla^2 \vec{u}, \\
\tag{2}&\nabla \cdot \vec{u} = 0, 
\end{align}$$
where the terms are the following:
* $\vec{x}$ - let that be the position vector which in 2D is just $\vec{x} = (x,y)$. 
* $\rho$ - constant density of the fluid,
* $\nu$ - the viscosity constant (the rate of internal friction),
* $\vec{u} = \vec{u}(\vec{x}, t)$ - the vector field of velocities,
* $p = p(\vec{x},t)$ - the scalar-valued pressure field,
* $\vec{F}_{\text{external}}$ - vector field of external forces such as gravity, bouyant forces or other forces.

The NS equations $(1)$ - $(2)$ say the following:
$$\begin{align}
& \underbrace{ \frac{\partial\vec{u}}{\partial t} }_{ \text{the acceleration} } + \underbrace{ (\vec{u}\cdot \nabla)\vec{u} }_{ \text{self-advection} }= \underbrace{ \frac{-\nabla p}{\rho} }_{ \text{the pressure gradient} } + \underbrace{ \frac{\vec{F}_{\text{external}}}{\rho} }_{ \text{applied external force} } + \underbrace{ \nu \nabla^2 \vec{u}, }_{ \text{the viscous force} } \\
& \underbrace{ \nabla \cdot \vec{u} = 0 ,}_{ \text{zero-divergence condition} } 
\end{align}$$
which in words can be stated as the acceleration of a fluid is equal to the sum of the negative pressure gradient, external forces and the viscosity (e.g the internal friction) and the fluid is contiuous. The zero-divergence condition that fluid is continuous e.g that in the simulation there are no unintentional sources or sinks of fluids (these will be intentionally programmed).  

Let the time be fixed ($t = \text{const.}$). Then let $\vec{u}(x,y) = (u_{x}(x,y), u_{y}(x,y))$ is a vector field with a vector argument representing vector field of velocities at that fixed time, and let $p(x,y)$ be a scalar field representing pressures of the fluid at that time. The vector calculus operators that are used in NS equations are the gradient of a scalar field which is a vector of directional derivatives in each basis vector direction $$ \tag{3} \nabla f(x,y) = \begin{pmatrix}
\frac{\partial \vec{V}}{\partial x} \\
\frac{\partial \vec{V}}{\partial y}
\end{pmatrix},$$the divergence of a vector field is a scalar field which is a dot product between a nabla operator vector (for 2D $\left( \frac{\partial}{\partial x}, \frac{\partial}{\partial y} \right)$) with a vector vector field 
$$\tag{4}\begin{align}
\nabla \cdot \vec{u}(x,y) = \frac{\partial u_{x}}{\partial x} + \frac{\partial u_{y}}{\partial y},
\end{align}$$
and a $\nabla^{2}\vec{u}$ is a Laplacian of a velocity field produces which is in explicit form
$$\tag{5}\begin{align}
\nabla^{2} \vec{u} = \left(  \frac{\partial^{2} u_{x}}{\partial x^{2}} +  \frac{\partial^{2} u_{y}}{\partial y^{2}} , \frac{\partial^{2} u_{x}}{\partial x^{2}} +  \frac{\partial^{2} u_{y}}{\partial y^{2}} \right).
\end{align}$$

Although these are analytical expressions that can be applied for analytically defined functions, the evolution of fluids can almost never be analytically defined especially in simulations where the fluid is represented discretely. Therefore these oprators aree applied on discretely defined scalar fields using the [finite difference methods](https://en.wikipedia.org/wiki/Finite_difference_method). For that, the fields $\vec{u}(x,y)$ and $p(x,y)$ must first be discretized. 
## Discrete Representation of Variables and Operators

### Discretization Procedure for a Simple Scalar Field

Let's say we want to discretely evaluate (solve) in a rectangular spatial domain $R$ and time $t>0$ some type of function $f(x,y,t)$ which is governed by some partial differential equation. Let $R$ be defined as
$$\begin{align}
R = \{(x,y) \in \mathbb{R}| \ 0<x<a, 0<y<a\}, \quad a = \text{const.} >0.
\end{align}$$
In finite-difference methods for solving PDE-s in 2D, the spatial domain is usually discretized by some fixed smallest spatial step for all coordinates as $\Delta x = h_{x} >0, \Delta y = h_{y} >0$. Time is also discretized to consist of smallest possible timesteps $\Delta t >0$. To assure the integer number of smallest spaces in a domain $R$, the smallest steps are defined by the integer numbers of cells in each direction. Let that be an integer constant $N>0$ for both axes. Then the following can be stated about the spatial discretization:

$$\begin{align}
&\Delta x = \Delta y = \frac{a}{N}
\end{align}$$
and the position vector $\vec{x} =(x,y)$ coordinates in the domain can be expressed for some integer $0\leq i\leq N$ as
$$\begin{align}

&x_{i} = i\Delta x, \\
&y_{i} = i\Delta y,
\end{align}$$
therefore there are $N+1$ nodes from $n=0$ to $n=N$ where a scalar field $f(x,y) =f(x_{i}, y_{j})$ can be evaluated therefore all the $(N+1)(N+1)$ values of $f(x_{i},y_{i})$ can be represented as a matrix $$\tag{6}\begin{align}
f_{ij}=f(x_{j},y_{i}) = \begin{pmatrix}
f(x_{0},y_{0}) & f(x_{1},y_{0}) & \dots&f(x_{n},y_{0}) \\
f(x_{0},y_{1}) & f(x_{1},y_{1}) & \dots & f(x_{n},y_{0}) \\
\vdots&\vdots&\ddots&\vdots \\
f(x_{0},y_{n}) & f(x_{1},y_{n}) & \dots & f(x_{n},y_{n})
\end{pmatrix}.
\end{align}$$
### Discretization Procedure for a Velocity Field

In computational fluid dynamics, the pressure is (implicitly) discretized such as a scalar field in equation $(6)$. However, to ensure the numerical ability to create 0-divergence, a staggered grid will be used to map velocity field components on the edges of cells that surround some pressure values. Therefore, if pressure for example takes on a matrix $(p)_{i,j}$ with dimensions $(N_{y}) \times (N_{x})$ in the form $$(p)_{i,j} = \begin{pmatrix}
p_{00}&p_{01}&\dots&p_{0,N_{x}-2}&p_{0,N_{x}-1} \\
p_{10}&p_{11}&\dots&p_{1,N_{x}-2}&p_{1,N_{x}-1} \\
\dots&\dots&\ddots&\vdots \\
p_{N_{y},0}&p_{N_{y},1}&\dots&p_{N_{y}-2,N_{x}-2}&p_{N_{y}-2,N_{x}-1} \\
p_{N_{y}+1,0}&p_{N_{y}+1,1}&\dots&p_{N_{y}-1,N_{x}-2}&p_{N_{y}-1,N_{x}-1}
\end{pmatrix} \in \mathbb{R}^{N_{y}\times N_{x}},$$
then the corresponding velocity fields $(u_{x})_{k,l}$ and $(u_{y})_{m,n}$ will be created such that each pressure value $p_{ij} = (p)_{i,j}$ will have two vertical neighbors that correspond to the vertical velocities $(u_{y})_{i,j}$ its cell and $(u_{y})_{i+1,j}$ below, and two horizontal neighbors that correspond to the horizontal velocities $(u_{x})_{i,j}$ from the left and $(u_{x})_{i,j+1}$ from the right.

Therefore $$\begin{align}
&(u_{x})_{k,l} \in \mathbb{R}^{N_{y}\times (N_{x}+1)}, \\
&(u_{t})_{m,n} \in \mathbb{R}^{(N_{y}+1)\times N_{x}}.
\end{align}$$
````cs
public class FluidGrid{
	//number of cells in each direction on a rectangular grid
	public readonly int Nx;
	public readonly int Ny;
	public readonly float h; //"infinitesimal" value dx=dy=h
	
	public readonly float[,] u_x;
	public readonly float[,] u_y;
	
	//initialization method
	public FluidGrid(int cellCountX, int cellCountY, float delta)
	{
		Nx = cellCountX;
		Ny = cellCountY;
		h = delta;
		
		//intialize velocity fields
		u_x = new float[N_y,N_x+1];
		u_y = new float[N_y+1, N_x];
	}
}
````

### Update Loop of the Stable Fluids Algorithm

The simulation will start with initial conditions $\vec{u}(\vec{x},0) = \vec{u}_{0}$. Let the solution of the algorithm after some time $t$ be $\vec{u}(\vec{x}, t) = \vec{w}_{0}(\vec{x})$.Then the update loop of the Stable Fluids simulation algorithm proceeds as follows:
$$\tag{7}\vec{w}_{0}(\vec{x}) \overbrace{ \to }^{ \text{add force} } \vec{w}_{1}(\vec{x}) \overbrace{ \to }^{ \text{advect} } \vec{w}_{2}(\vec{x}) \overbrace{ \to }^{ \text{diffuse} } \vec{w}_{3}(\vec{x}) \overbrace{ \to }^{ \text{project} } \vec{w}_{4}(\vec{x}).$$
The first step will be the **addition of external force**:
$$\vec{w}_{1}(\vec{x}) = \vec{w}_{0}(\vec{x})+\Delta t \vec{f}(\vec{x},t),$$
where $\vec{f}$ is some external force field normalised by the density.

The second step will be the **advection** which in numerical implementations is simply shifting the velocity field values along the velocity field lines by some small amount using *the method of characteristics*:

$$\vec{w}_{2}(\vec{x}) \leftarrow  \vec{w}_{1}(\vec{p}(\vec{x}, t-\Delta t)).$$
For the next step **viscous diffusion** will be implemented which is equivalent to a diffusion equation $$\frac{\partial \vec{w}_{2}}{\partial t} = \nu \nabla^{2} \vec{w}_{2}$$
which will be solved using standard implicit methods that result in a sparse linear equation $$(\mathbf{I}-\nu \Delta t \nabla^{2}) \vec{w}_{3}(\vec{x}) = \vec{w}_{2}(\vec{x}).$$
As the last step in the calculation rule $(7)$ the **projection** occurs. The resulting $\vec{w}_{3}$ gained diverging part $\nabla q$ in addition to the desired field component that contributes the curl $\vec{w}_{3}$ in the form $$\vec{w}_{3} = \vec{w}_{4}+ \nabla q,$$
where $q$ is a scalar field that obeys a Poisson equation $$\nabla^{2} q = \nabla \cdot \vec{w}_{3}.$$
A solution of this equation in the domain $R$ with neumann boundary condition $\frac{\partial q}{\partial n} = 0$ will be used to derive the projection for $\vec{w}_{4}$ in the form
$$\vec{w}_{4} = \mathbf{P} \vec{w}_{3}.$$
### Update Loop Functions in Code - TODO
````cs

public void Awake(){
	//initialization process
}

public void Update(){

	// 1. 
	AddForces(u, forceField, dt);
	
	// 2. Semi-Lagrangian advection which means advecting velocities by itself
	Advect(u, u, dt);
	
	// 3. Implicit diffusion using Gauss-Seidel
	Diffuse(u, viscosity, dt, iterations);
	
	// 4. Projection step
	Project(u, iterations);
}
````

### Solving for Diffusion

The diffusion step is based on the diffusion equation $$\frac{\partial \vec{w}_{2}}{\partial t} = \nu \nabla^{2}\vec{w}_{2}$$
which in backward finite difference discrete form will be for  $(\vec{w}_{2})_{i,j} = ((w_{2,x})_{i,j},(w_{2,y})_{i,j})$

$$\begin{align}
&\frac{(\vec{w}_{2})^{\text{new}}-(\vec{w}_{2})^{\text{old}}}{\Delta t} = \nu \nabla^{2} (\vec{w}_{2})^{new}, \\
\tag{8}\implies& (I - \nu \Delta t \nabla^{2})(\vec{w}_{2})^{\text{new}}= (\vec{w}_{2})^{\text{old}},\quad (\vec{w}_{2})^{new} = \vec{w}_{3}.
\end{align}$$
Therefore solving the implicit system of equations $(8)$ will yield the

To solve this in a square domain with no inner boundaries, a finite difference diffusion operator $$\nabla^{2} = \frac{1}{\Delta x^{2}} \begin{pmatrix}
-2&1&0&\dots & \dots&0 \\
1&-2&1&0&\dots&0 \\
\vdots&\vdots&\vdots&\vdots&\ddots&\vdots& \\
0&0&0&1&-2&1 \\
0&0&0&0&1&-2
\end{pmatrix}$$
is used. This kind of sparse system is simple to solve.



### Solving for Projection

Projection or in other words 