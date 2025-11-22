
To enforce incompressibility and remove the changes in pressure, we attempt to zero out the divergence throughout the flow field. This is done by adding flow away from high pressure converging areas, and toward low pressure diverging areas. A gradient is the slope or rate of a value across a grid. The gradient of the divergence is a vector field that points from high pressure to low pressure areas. So to remove the divergence from a flow field, we repeatedly increment it by the gradient of its divergence.

We can combine the divergence and gradient calculations, and if we find divergence values at the corners between grid cells instead of at their centers, we can use a simple 3x3 convolution for this.


### The Navier Stokes Equations

The Navier-Stokes equations for incompressible constant density fluids:
$$\begin{align}
& \frac{\partial \vec{u}}{\partial t} = - \frac{\nabla p}{\rho} - (\vec{u} \cdot \nabla)\vec{u} + \nu \nabla^{2} \vec{u} + \vec{f}, \\&\nabla \cdot \vec{u} = 0.
\end{align}$$

### Discretized Fields

We have discretized the grid into $N_{y} \times N_{x}$ cells such that each cell has their own (implicit) discrete $(p)_{i,j}$ values in the center and each cell has velocity field values $(u_{y})_{i\pm \frac{1}{2},j}$ and $(u_{x})_{i, j\pm \frac{1}{2}}$ on the corresponding edges that correspond to the velocity components entering and exiting the cell. Therefore the dimensions of each of these matrices will be:
$$\begin{align}
& (p)_{i,j} \in \mathbb{R}^{N_{y} \times N_{x}}, \\
& (u_{x})_{i \pm \frac{1}{2}, j} \in \mathbb{R}^{(N_{y}+1) \times N_{x}}, \\
&(u_{y})_{i,j\pm \frac{1}{2}} \in \mathbb{R}^{N_{y}\times (N_{x}+1)}.
\end{align}$$
(The half indices are used just to with respect to the cell indices). Each cell a has spatial dimension $\Delta x \times \Delta y$ where $\Delta x = \Delta y = h$, i.e it is the smallest spatial ("approximated infinitesimal") spacing parameter that will be used in measuring the domain and calculating spatial finite differences. Minimal time evolution spacing i.e the time between each physical frame or tick will be $\Delta t$.

### Boundary Masks

To make the simulation more interesting, boundaries will be added within the simulation domain. Let $(D)_{i,j} \in \mathbb{R}^{N_{y} \times N_{x}}$ be the spatial domain matrix of the simulation where each coordinate corresponds to the $i,j$-th cell. In programming, it could just be a bit-matrix where one-values indicate, that this cell is a part of the fluid, and zero-values show, that the cell is a part of some obstacle.
Therefore within the update loop, the following check will be applied:

$$\begin{align}
\text{if } (D)_{i,j} = 0 \; \text{then } (u_{x})_{i,j\pm \frac{1}{2}}= (u_{y})_{i \pm \frac{1}{2}, j} = 0.  \\
\end{align}$$
## Frame Update

Let $k$ be the timestep, such that the current time of the simulation is $t_{k} = k\Delta t$ and the next step will be $t_{k+1} = (k+1) \Delta t$. Since some $N$ operations will be applied on the velocity fields during one update loop, let $l = 1,2,\dots, N$ be the index refering to the current step such that the transformation of velocity fields will be the following:
$$\begin{align}
& (u_{x})_{i,j\pm \frac{1}{2}}^{[k]} \to (u_{x})_{i,j\pm \frac{1}{2}}^{1} \to (u_{x})_{i,j\pm \frac{1}{2}}^{2} \to \dots \to(u_{x})_{i,j\pm \frac{1}{2}}^{l} \to \dots \to (u_{x})_{i,j\pm \frac{1}{2}}^{N} = (u_{x})_{i,j\pm \frac{1}{2}}^{[k+1]}, \\
&(u_{y})_{i\pm \frac{1}{2}, j}^{[k]} \to (u_{y})_{i\pm \frac{1}{2},j}^{1} \to (u_{y})_{i\pm \frac{1}{2},j}^{2} \to \dots \to(u_{y})_{i\pm \frac{1}{2},j}^{l} \to \dots \to (u_{y})_{i\pm \frac{1}{2},j}^{N} = (u_{y})_{i\pm \frac{1}{2}, j}^{[k+1]},
\end{align}$$
where each arrow between some $l$-th and $l+1$-st value corresponds to the specific update step.

### Step I: Addition of Forces

Firstly, the contribution of the forcing term $\vec{f}$, which acts as acceleration, will be added to the velocity fields $(u_{x})_{i,j\pm \frac{1}{2}}, (u_{y})_{i\pm \frac{1}{2},j}$. The forcing term could be some preprogrammed time-dependent field or could instead be dependent on the user-input - such as applying some acceleration brush or moving the boundaries, former of which will be tricky to implement.
The velocity change contribution from force will be acceleration times the time-step.

Let the discrete form of the forcing term $\vec{f}$ be denoted as $a$ such as acceleration usually is. Then the discrete matrices of the acceleration values will have the same shape as the corresponding velocity fields i.e
$$\begin{align}
& (a_{x})_{i, j \pm \frac{1}{2}} \in \mathbb{R}^{N_{y}\times (N_{x}+1)}, \\
& (a_{y})_{i\pm \frac{1}{2}, j} \in \mathbb{R}^{(N_{y}+1) \times N_{x}}.
\end{align}$$
The first transformation will then be
$$\begin{align}
& (u_{x})_{i,j \pm \frac{1}{2}}^1 = (u_{x})_{i,j \pm \frac{1}{2}}^{[k]} + (a_{x})_{i, j \pm \frac{1}{2}}\Delta t , \\
& (u_{y})_{i\pm \frac{1}{2},j }^1 = (u_{y})_{i\pm \frac{1}{2},j }^{[k]} + (a_{y})_{i\pm \frac{1}{2}, j }\Delta t.
\end{align}$$
To make the notation more compact, let's rewrite the last set of four equations as $$(\mathbf{u})^1_{i,j} = (\mathbf{u})^{[k]}_{i,j} + (\mathbf{a})_{i,j}  \Delta t,$$
so that from now on, the expressions in bold indicate the four staggered values on the edges of each $i,j$-th cell.

If the places of added acceleration do not coincide with the boundaries then right-after the force application, the zero-velocity boundary conditions will be applied right after the iteration step.

### Step II: Advection

To advect velocity components for each $i,j$-th fluid cell's four edges, we sample a new value from $\mathbf{u}^1$ velocity field components from position $\mathbf{x}_{\text{edge}} - (\mathbf{u})^1_{\text{edge}} \Delta t$ so we basically backtrack and look for what possible value each face's velocity corresponds to. The backtracking will most of the times correspond to some arbitrary location with non-integer indices $(m+\Delta m, n + \Delta n)$ where corresponding $\Delta$-s are the fractional parts between $-\frac{1}{2}$ and $\frac{1}{2}$, so to sample from these indices, bilinear interpolation will be used.

Therefore for each edge's velocity
$$\mathbf{u}^2(\mathbf{x}) = \mathbf{u}^1(\mathbf{x} - \mathbf{u}^1(\mathbf{x}) \Delta t).$$

Let position vectors on the grid be defined as for some arbitrary index with fractional part $$\begin{pmatrix}
\mathbf{x}
\end{pmatrix}_{a,b} = \begin{pmatrix}
y_{a} \\
x_{b}
\end{pmatrix} = \begin{pmatrix}
ah \\
bh
\end{pmatrix},$$

where $h$ is the spacing of the grid.

Some arbitrary new position $\mathbf{x}_{a',b'} = \mathbf{x}_{a,b} - \mathbf{u}^1(\mathbf{x_{a,b}}) \Delta t$ is then
$$\begin{align}
&\begin{pmatrix}
y_{a'} \\
x_{b'}
\end{pmatrix} = \begin{pmatrix}
y_{a} \\
y_{b}
\end{pmatrix} -  \Delta t \begin{pmatrix}
(u_y)_{a,b} \\
(u_x)_{a,b}
\end{pmatrix}, \\
&\begin{pmatrix}
a'h \\
b'h
\end{pmatrix} = \begin{pmatrix}
ah \\
bh
\end{pmatrix} - \Delta t \begin{pmatrix}
(u_y)_{a,b} \\
(u_x)_{a,b}
\end{pmatrix}, \\
\implies & a' = a - \frac{1}{h} \Delta t (u_{y})_{a,b}, \\
&b' = b - \frac{1}{h} \Delta (u_{x})_{a,b}.
\end{align}$$
where new positions can be evaluated as

Therefore each edge's velocity is advected in the following way:
$$\begin{align}
&(u_{x})^2_{i,j\pm\frac{1}{2}} = (u_{x})^{1}_{m+\Delta m, n+\Delta n},\\
&(u_{y})^2_{i\pm\frac{1}{2}, j} = (u_{y})^{1}_{p+\Delta p, q+\Delta q},\\
\end{align}$$
where the sampling indices are

$$\begin{align}
&m+\Delta m =  i - \Delta t (u_{y})_{i, j\pm \frac{1}{2}}, \\
&n+\Delta n =  j - \Delta t (u_{x})_{i, j\pm \frac{1}{2}}, \\
&p+\Delta p =  i - \Delta t (u_{y})_{i \pm \frac{1}{2}, j}, \\
&q+\Delta q =  j - \Delta t (u_{x})_{i \pm \frac{1}{2}, j}. \\
\end{align}$$

Notice, how there are values of $(u_{x})_{i\pm \frac{1}{2},j}$ and $(u_{y})_{i, j\pm \frac{1}{2}}$ which aren't well defined to be some elements of any current grid. For finding these values, bilinear interpolation will be used. Also, the sampling indices will most certainly land on some arbitrary place in a cell, which calls for bilinear interpolation when sampling.