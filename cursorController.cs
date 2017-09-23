using UnityEngine;
using System.Collections;

using LockingPolicy = Thalmic.Myo.LockingPolicy;
using Pose = Thalmic.Myo.Pose;
using UnlockType = Thalmic.Myo.UnlockType;
using VibrationType = Thalmic.Myo.VibrationType;

// Orient the object to match that of the Myo armband.
// Compensate for initial yaw (orientation about the gravity vector) and roll (orientation about
// the wearer's arm) by allowing the user to set a reference orientation.
// Making the fingers spread pose or pressing the 'r' key resets the reference orientation.
public class cursorController : MonoBehaviour
{
	// Myo game object to connect with.
	// This object must have a ThalmicMyo script attached.
	public GameObject myo = null;

	// A rotation that compensates for the Myo armband's orientation parallel to the ground, i.e. yaw.
	// Once set, the direction the Myo armband is facing becomes "forward" within the program.
	// Set by making the fingers spread pose or pressing "r".
	private Quaternion _antiYaw = Quaternion.identity;

	// A reference angle representing how the armband is rotated about the wearer's arm, i.e. roll.
	// Set by making the fingers spread pose or pressing "r".
	private float _referenceRoll = 0.0f;

	// The pose from the last update. This is used to determine if the pose has changed
	// so that actions are only performed upon making them rather than every frame during
	// which they are active.
	private Pose _lastPose = Pose.Unknown;

	// Gain constants.
	private const float pixelDensity = 0.83f;
	private const float frameRate = 60.0f;
	private const float vMax = (float)Mathf.PI;
	private const float vMin = 0.174532925f;
	private const float CDMax = 4580.0f / (Mathf.PI / 6.0f);
	private const float CDMin = 16.0f / 0.274532925f;
	private const float inflectionRatioMin = 0.4f;
	private const float inflectionRatioMax = 0.7f;
	private const float lambdaMin = 4.0f / (vMax - vMin);
	private const float lambdaMax = 5.0f / (vMax - vMin);
	private const float cursorSensitivity = 0.5f;
	private const float cursorAcceleration = 0.3f;

	public float moveMultiplier = 0.01f;
	public float dyScale = 1.33f;

	// Update is called once per frame.
	void Update ()
	{
		// Access the ThalmicMyo component attached to the Myo object.
		ThalmicMyo thalmicMyo = myo.GetComponent<ThalmicMyo> ();

		// Update references when the pose becomes fingers spread or the q key is pressed.
		bool updateReference = false;
		if (thalmicMyo.pose != _lastPose) {
			_lastPose = thalmicMyo.pose;

			//if (thalmicMyo.pose == Pose.FingersSpread) {
				//updateReference = true;

				//ExtendUnlockAndNotifyUserAction (thalmicMyo);
			//}
		}

		if (Input.GetKeyDown ("r")) {
			updateReference = true;
		}

		if (thalmicMyo.pose == Pose.DoubleTap) {
			updateReference = true;
		}

		if (updateReference) {

			transform.SetPositionAndRotation(new Vector3 (333.0f, 180.0f, 0f), new Quaternion(0, 0, 0, 0));

			// _antiYaw represents a rotation of the Myo armband about the Y axis (up) which aligns the forward
			// vector of the rotation with Z = 1 when the wearer's arm is pointing in the reference direction.
			_antiYaw = Quaternion.FromToRotation (
				new Vector3 (myo.transform.forward.x, 0, myo.transform.forward.z),
				new Vector3 (0, 0, 1)
			);

			Vector3 referenceZeroRoll = computeZeroRollVector (myo.transform.forward);
			_referenceRoll = rollFromZero (referenceZeroRoll, myo.transform.forward, myo.transform.up);
		}

		Vector3 zeroRoll = computeZeroRollVector (myo.transform.forward);
		float roll = rollFromZero (zeroRoll, myo.transform.forward, myo.transform.up);
		float relativeRoll = normalizeAngle (roll - _referenceRoll);
		Quaternion antiRoll = Quaternion.AngleAxis (relativeRoll, myo.transform.forward);
		transform.rotation = _antiYaw * antiRoll * Quaternion.LookRotation (myo.transform.forward);

		Vector2 dxdy = gyroConversion (thalmicMyo.gyroscope, myo.transform.rotation, thalmicMyo.xDirection);

		float dx = updateMouseDeltas (dxdy [0], dxdy [1]) [0];
		float dy = updateMouseDeltas (dxdy [0], dxdy [1]) [1];

		transform.Translate (dx, dy * dyScale, 0, Space.World);
	}


	Vector2 gyroConversion (Vector3 gyroData, Quaternion orientation, object xdir)
	{
		// Convert gyro data from the gyroscope's frame of reference to the world frame of reference.
		// Rotate gyroData by orientation quaternion.
		Vector3 gyroRad = new Vector3 (Mathf.Deg2Rad * gyroData.x, Mathf.Deg2Rad * gyroData.y, Mathf.Deg2Rad * gyroData.z);
		Vector3 gyroWorldData = orientation * gyroRad;

		// Define the world forward vector.
		Vector3 forwardSource = new Vector3 (0, 0, -1);

		// TODO: Make Myo work both flipped and not flipped. For now it's ok.
		if (xdir.Equals (Thalmic.Myo.XDirection.TowardElbow)) {
			Vector3 temp = new Vector3 (0, 0, -1);
			forwardSource = temp;
		}

		// Myo's current forward vector.
		Vector3 forward = orientation * forwardSource;

		// Myo's current right vector.
		Vector3 right = Vector3.Cross(forward, new Vector3 (0, -1, 0));

		// Define the world up vector.
		Vector3 up = new Vector3 (1, 0, 0);

		Quaternion yQuat = Quaternion.FromToRotation(right, up);

		float m = Mathf.Sqrt(yQuat.w * yQuat.w +
			yQuat.x * yQuat.x +
			yQuat.y * yQuat.y +
			yQuat.z * yQuat.z);

		Quaternion yCompNorm = new Quaternion (yQuat.w / m, yQuat.x / m, yQuat.y / m, yQuat.z / m);

		Vector3 gyroCompensated = yCompNorm * gyroWorldData;

		Vector2 coordinates = new Vector2 (-gyroWorldData.y, -gyroCompensated.x);

		return coordinates;

	}
		
	float getGain (float deviceSpeed, float sensitivity, float acceleration)
	{
		float inflectionVelocity = sensitivity * (vMax - vMin) + vMin;
		float CDGain = CDMin + (CDMax - CDMin) / (1 + Mathf.Exp (-acceleration * (deviceSpeed - inflectionVelocity)));
		float returnedGain = CDGain * pixelDensity * moveMultiplier;
		return returnedGain;
	}

	Vector2 updateMouseDeltas (float dx, float dy)
	{
		float frameDuration = 1.0f / frameRate;
		float norm = Mathf.Sqrt (dx * dx + dy * dy);
		float gain = getGain (norm, cursorSensitivity, cursorAcceleration);
		float scaled_dx = dx * gain * frameDuration;
		float scaled_dy = dy * gain * frameDuration;

		Vector2 updatedDeltas = new Vector2 (scaled_dx, scaled_dy);
		return updatedDeltas;
	}

	// Compute the angle of rotation clockwise about the forward axis relative to the provided zero roll direction.
	// As the armband is rotated about the forward axis this value will change, regardless of which way the
	// forward vector of the Myo is pointing. The returned value will be between -180 and 180 degrees.
	float rollFromZero (Vector3 zeroRoll, Vector3 forward, Vector3 up)
	{
		// The cosine of the angle between the up vector and the zero roll vector. Since both are
		// orthogonal to the forward vector, this tells us how far the Myo has been turned around the
		// forward axis relative to the zero roll vector, but we need to determine separately whether the
		// Myo has been rolled clockwise or counterclockwise.
		float cosine = Vector3.Dot (up, zeroRoll);

		// To determine the sign of the roll, we take the cross product of the up vector and the zero
		// roll vector. This cross product will either be the same or opposite direction as the forward
		// vector depending on whether up is clockwise or counter-clockwise from zero roll.
		// Thus the sign of the dot product of forward and it yields the sign of our roll value.
		Vector3 cp = Vector3.Cross (up, zeroRoll);
		float directionCosine = Vector3.Dot (forward, cp);
		float sign = directionCosine < 0.0f ? 1.0f : -1.0f;

		// Return the angle of roll (in degrees) from the cosine and the sign.
		return sign * Mathf.Rad2Deg * Mathf.Acos (cosine);
	}

	// Compute a vector that points perpendicular to the forward direction,
	// minimizing angular distance from world up (positive Y axis).
	// This represents the direction of no rotation about its forward axis.
	Vector3 computeZeroRollVector (Vector3 forward)
	{
		Vector3 antigravity = Vector3.up;
		Vector3 m = Vector3.Cross (myo.transform.forward, antigravity);
		Vector3 roll = Vector3.Cross (m, myo.transform.forward);

		return roll.normalized;
	}

	// Adjust the provided angle to be within a -180 to 180.
	float normalizeAngle (float angle)
	{
		if (angle > 180.0f) {
			return angle - 360.0f;
		}
		if (angle < -180.0f) {
			return angle + 360.0f;
		}
		return angle;
	}

	// Extend the unlock if ThalmcHub's locking policy is standard, and notifies the given myo that a user action was
	// recognized.
	void ExtendUnlockAndNotifyUserAction (ThalmicMyo myo)
	{
		ThalmicHub hub = ThalmicHub.instance;

		if (hub.lockingPolicy == LockingPolicy.Standard) {
			myo.Unlock (UnlockType.Timed);
		}

		myo.NotifyUserAction ();
	}
}