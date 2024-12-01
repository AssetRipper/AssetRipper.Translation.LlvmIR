typedef struct
{
    float x;
    float y;
    float z;
} Vector3F;

extern "C" {
    Vector3F vector3f_add(Vector3F left, Vector3F right)
    {
        Vector3F result;
        result.x = left.x + right.x;
        result.y = left.y + right.y;
        result.z = left.z + right.z;
        return result;
    }

    Vector3F vector3f_pointer_add(Vector3F* left, Vector3F* right)
    {
        return vector3f_add(*left, *right);
    }

    Vector3F Vector3F_add_3(Vector3F v1, Vector3F v2, Vector3F v3)
    {
        Vector3F result;
        result.x = v1.x + v2.x + v3.x;
        result.y = v1.y + v2.y + v3.y;
        result.z = v1.z + v2.z + v3.z;
        return result;
    }

    Vector3F Vector3F_add_4(Vector3F v1, Vector3F v2, Vector3F v3, Vector3F v4)
    {
        Vector3F result;
        result.x = v1.x + v2.x + v3.x + v4.x;
        result.y = v1.y + v2.y + v3.y + v4.y;
        result.z = v1.z + v2.z + v3.z + v4.z;
        return result;
    }

    Vector3F Vector3F_add_5(Vector3F v1, Vector3F v2, Vector3F v3, Vector3F v4, Vector3F v5)
    {
        Vector3F result;
        result.x = v1.x + v2.x + v3.x + v4.x + v5.x;
        result.y = v1.y + v2.y + v3.y + v4.y + v5.y;
        result.z = v1.z + v2.z + v3.z + v4.z + v5.z;
        return result;
    }
}