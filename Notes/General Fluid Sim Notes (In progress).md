These notes are based on the following resources:
* [Fluid Flow Tutorial by Karl Sims](https://www.karlsims.com/fluid-flow.html)
* [Coding Adventure: Simulating Smoke by Sebastian Lague](https://www.youtube.com/watch?v=Q78wvrQ9xsU)
* [Stable Fluids by Jos Stam](https://pages.cs.wisc.edu/~chaol/data/cs777/stam-stable_fluids.pdf)
## The Theory and Implementation
Implementation notes are written in pseudo-code.
### Navier Stokes Equations for Incompressible fluids
Stable fluids assumes that the simulated fluid is uniformly viscous and non-compressible e.g its density is constant across the domain and time. Although real fluids are not incompressible, it is still a good approximation for simulating many real fluids such that water or gases which usually have low compressibility factor when the conditions such as pressure or temperature are not fluctuating too much.

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
\nabla^{2} \vec{u} = \left(  \frac{\partial^{2} u_{1}}{\partial x^{2}} +  \frac{\partial^{2} u_{1}}{\partial y^{2}} , \frac{\partial^{2} u_{2}}{\partial x^{2}} +  \frac{\partial^{2} u_{2}}{\partial y^{2}} \right).
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
therefore there are $N+1$ nodes from $n=0$ to $n=N$ where a scalar field $f(x,y) =f(x_{i}, y_{j})$ can be evaluated therefore all the $(N+1)(N+1)$ values of $f(x_{i},y_{i})$ can be represented as a matrix $$\begin{align}
f_{ij}=f(x_{j},y_{i}) = \begin{pmatrix}
f(x_{0},y_{0}) & f(x_{1},y_{0}) & \dots&f(x_{n},y_{0}) \\
f(x_{0},y_{1}) & f(x_{1},y_{1}) & \dots & f(x_{n},y_{0}) \\
\vdots&\vdots&\ddots&\vdots \\
f(x_{0},y_{n}) & f(x_{1},y_{n}) & \dots & f(x_{n},y_{n})
\end{pmatrix}.
\end{align}$$
### Discretization Procedure for a Velocity Field

Since in the equation $(2)$ we are interested in



````cs
public class FluidGrid{
	//number of cells in each direction on a square grid
	public readonly int Nx;
	public readonly int Ny;
	public float
	//...
}
````


### Update Loop of the Stable Fluids Algorithm

The simulation will start with initial conditions $\vec{u}(\vec{x},0) = \vec{u}_{0}$. Let the solution of the algorithm after some time $t$ be $\vec{u}(\vec{x}, t) = \vec{w}_{0}(\vec{x})$.Then the update loop of the Stable Fluids simulation algorithm proceeds as follows:
$$\vec{w}_{0}(\vec{x}) \overbrace{ \to }^{ \text{add force} } \vec{w}_{1}(\vec{x}) \overbrace{ \to }^{ \text{advect} } \vec{w}_{2}(\vec{x}) \overbrace{ \to }^{ \text{diffuse} } \vec{w}_{3}(\vec{x}) \overbrace{ \to }^{ \text{project} } \vec{w}_{4}(\vec{x}).$$
The first step will be the addition of external force:
$$\vec{w}_{1}(\vec{x}_{1}) = \vec{w}_{0}(\vec{x})+\Delta t \vec{f}(\vec{x},t),$$
where $\vec{f}$ is some external force field normalised by the density.

The second step will be the adv