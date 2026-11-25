using UnityEngine;

namespace NFWL
{
    /// <summary>The net's spring network as plain math (shared by GoalNet and the tests).</summary>
    public static class NetSpring
    {
        /// <summary>Advances displacement / velocity of a grid of net knots by dt (two sub-steps,
        /// semi-implicit Euler: stable for the default constants at 30 FPS and above). Returns the energy left.</summary>
        public static float Step(float[] disp, float[] vel, bool[] pinned, int cols, int rows,
            float stiffness, float damping, float coupling, float maxBulge, float dt)
        {
            float h = dt * 0.5f;
            float energy = 0f;
            for (int s = 0; s < 2; s++)
            {
                for (int r = 1; r < rows - 1; r++)
                for (int c = 1; c < cols - 1; c++)
                {
                    int i = r * cols + c;
                    if (pinned[i]) continue;
                    float lap = disp[i - 1] + disp[i + 1] + disp[i - cols] + disp[i + cols] - 4f * disp[i];
                    vel[i] += (-stiffness * disp[i] - damping * vel[i] + coupling * lap) * h;
                }
                energy = 0f;
                for (int i = 0; i < disp.Length; i++)
                {
                    if (pinned[i]) continue;
                    disp[i] = Mathf.Clamp(disp[i] + vel[i] * h, -maxBulge * 0.3f, maxBulge);
                    energy += Mathf.Abs(disp[i]) + Mathf.Abs(vel[i]) * 0.1f;
                }
            }
            return energy;
        }
    }
}
