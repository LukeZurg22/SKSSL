using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
// ReSharper disable UnusedMember.Global

namespace Sin3D._Camera3D;

/// <summary>
/// A 3D camera class that handles view and projection matrices.
/// </summary>
public class Camera3D
{
    /// <summary>
    /// The (x, y, z) position.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// The yaw (in radians).
    /// </summary>
    public float Yaw { get; set; }

    /// <summary>
    /// The pitch (in radians).
    /// </summary>
    public float Pitch { get; set; }

    /// <summary>
    /// The roll (in radians).
    /// </summary>
    public float Roll { get; set; }

    private float fov;

    /// <summary>
    /// The field of view (in radians), (the setter will only assign fov values between 0 and PI).
    /// </summary>
    public float Fov
    {
        get => fov;
        set
        {
            if (value > 0 && value < Math.PI)
            {
                fov = value;
            }
        }
    }

    /// <summary>
    /// The near plane render distance (very small values could impact depth buffer precision).
    /// </summary>
    public float NearPlaneDist { get; set; }

    /// <summary>
    /// The far plane render distance.
    /// </summary>
    public float FarPlaneDist { get; set; }

    /// <summary>
    /// The view matrix.
    /// </summary>
    public Matrix ViewMatrix { get; private set; }

    /// <summary>
    /// The projection matrix.
    /// </summary>
    public Matrix ProjectionMatrix { get; private set; }

    /// <summary>
    /// Creates a new Camera3D object with position, rotation, fov and near/far plane render distance settings.
    /// </summary>
    /// <param name="position">The initial (x, y, z) position.</param>
    /// <param name="rotation">The initial (yaw, pitch, roll) rotation.</param>
    /// <param name="fov">The initial field of view in degrees. (E.g. 45, 90, etc.)</param>
    /// <param name="nearPlaneDist">The initial near plane render distance.</param>
    /// <param name="farPlaneDist">The initial far plane render distance.</param>
    /// <param name="_graphicsDevice">The graphics device, used in creating the projection matrix.</param>
    public Camera3D(
        Vector3 position,
        Vector3 rotation,
        GraphicsDevice _graphicsDevice,
        float fov = 90f, // Keep at current value!
        float nearPlaneDist = 0.01f, // Keep at current value!
        float farPlaneDist = 100) // Keep at current value!
    {
        Position = position;

        Yaw = rotation.X;
        Pitch = rotation.Y;
        Roll = rotation.Z;

        Fov = MathHelper.ToRadians(fov);
        NearPlaneDist = nearPlaneDist;
        FarPlaneDist = farPlaneDist;

        //setting up the view and projection matrices
        UpdateViewMatrix();
        ProjectionMatrix =
            Matrix.CreatePerspectiveFieldOfView(Fov, _graphicsDevice.Viewport.AspectRatio, nearPlaneDist, farPlaneDist);
    }

    /// <summary>
    /// Create a Camera that automatically handles the parameters of the main constructor.
    /// </summary>
    /// <param name="graphicsDevice"></param>
    /// <returns>New Camera Instance</returns>
    public static Camera3D Default(GraphicsDevice graphicsDevice) => new(Vector3.Zero, Vector3.Zero, graphicsDevice);

    /// <summary>
    /// Resets camera's position to (0,0,0)
    /// </summary>
    public void ResetPosition() => Position = new Vector3(0, 0, 0);

    /// <summary>
    /// Updates the camera's view matrix (to be used after changing position or rotation).
    /// </summary>
    public void UpdateViewMatrix()
    {
        //getting cam target
        Matrix rotationMatrix = Matrix.CreateFromYawPitchRoll(Yaw, Pitch, Roll);
        Vector3 direction = Vector3.Transform(Vector3.Forward, rotationMatrix);
        Vector3 target = direction + Position;

        ViewMatrix = Matrix.CreateLookAt(Position, target, Vector3.Up);
    }

    /// <summary>
    /// Updates the camera's projection matrix (to be used after changing fov or the near/far plane distance).
    /// </summary>
    /// <param name="_graphicsDevice">The graphics device used in the creation of the projection matrix.</param>
    public void UpdateProjectionMatrix(GraphicsDevice _graphicsDevice)
    {
        ProjectionMatrix =
            Matrix.CreatePerspectiveFieldOfView(fov, _graphicsDevice.Viewport.AspectRatio, NearPlaneDist, FarPlaneDist);
    }
}