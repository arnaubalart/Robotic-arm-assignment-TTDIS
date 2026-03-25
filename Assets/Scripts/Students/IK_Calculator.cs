using UnityEngine;

public class IK_Calculator
{
    private readonly float L1;
    private readonly float L2;
    private readonly float L3;

    public IK_Calculator(float l1, float l2, float l3)
    {
        L1 = l1; L2 = l2; L3 = l3;
    }

    public Vector3 Calculate(Vector3 wristTarget)
    {
        if (wristTarget == Vector3.zero) return Vector3.zero;

        float wx = wristTarget.x;
        float wy = wristTarget.y;
        float wz = wristTarget.z;

        // Ipotenusa 
        float hyp = Mathf.Sqrt((wx * wx) + (wz * wz));
        float a = Mathf.Sqrt((hyp * hyp) + Mathf.Pow(wy - L1, 2));

        // --- 1 (BASE) ---
        float y1 = Mathf.Atan2(wx, wz) * Mathf.Rad2Deg;

        // --- 3 (KNEE) ---
        float cosY3 = (Mathf.Pow(L2, 2) + Mathf.Pow(L3, 2) - Mathf.Pow(a, 2)) / (2.0f * L2 * L3);
        cosY3 = Mathf.Clamp(cosY3, -1f, 1f);
        float interiorY3 = Mathf.Acos(cosY3) * Mathf.Rad2Deg;
        float y3 = -(180f - interiorY3);

        // --- GIUNTO 2 (SHOULDER) ---
        float y22 = Mathf.Atan2(wy - L1, hyp) * Mathf.Rad2Deg;
        float cosY23 = (Mathf.Pow(a, 2) + Mathf.Pow(L2, 2) - Mathf.Pow(L3, 2)) / (2.0f * a * L2);
        cosY23 = Mathf.Clamp(cosY23, -1f, 1f);
        float y23 = Mathf.Acos(cosY23) * Mathf.Rad2Deg;

        
        float y2 = y22 + y23;

        return new Vector3(y1, y2, y3);
    }
}