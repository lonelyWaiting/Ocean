# Ocean
FFT Ocean Implement of "Simulation Ocean Water Jerry Tessendorf"

[Ocean](./ocean_effect.png)

# Algorithm

- 根据`Phillips Specturm`在频率域实现波形
- 使用IFFT变换到时域
- 生成Displacement Map
- 生成网格,使用Displacement Map修改顶点位置,实现海面运动