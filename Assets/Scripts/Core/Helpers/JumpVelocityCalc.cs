using UnityEngine;

public static class JumpVelocityCalc
{
    //Found math online
    public static Vector3 CalculateJumpVelocity(Vector3 startPos, Vector3 endPos, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPos.y - startPos.y;
        Vector3 displacementXZ = new Vector3(endPos.x - startPos.x, 0f, endPos.z - startPos.z);


        float terminalHeight = Mathf.Max(displacementY, 0) + 0.5f;
        float actualHeight = Mathf.Max(trajectoryHeight, terminalHeight);


        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * actualHeight);


        float timeToPeak = Mathf.Sqrt(-2 * actualHeight / gravity);
        float timeFromPeakToTarget = Mathf.Sqrt(2 * (displacementY - actualHeight) / gravity);

        Vector3 velocityXZ = displacementXZ / (timeToPeak + timeFromPeakToTarget);

        return velocityXZ + velocityY;
    }
}