using System.Net;
using UnityEngine;

public static class JumpVelocityCalc 
{
    static public Vector3 CalculateJumpVelocity(Vector3 startPos, Vector3 endPos, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float YDisplacement = endPos.y - startPos.y;
        Vector3 dispalcementXZ = new Vector3(endPos.x - startPos.x, 0f,endPos.z - startPos.z);  

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * trajectoryHeight);
        Vector3 velocityXZ = dispalcementXZ / (Mathf.Sqrt(-2 * trajectoryHeight / gravity) + Mathf.Sqrt(2 * (YDisplacement - trajectoryHeight) / gravity));

        return velocityXZ + velocityY;
    }
}
