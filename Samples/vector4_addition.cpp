typedef struct
{
    float x;
    float y;
    float z;
    float w;
} Vector4F;

extern "C" {
    Vector4F vector4f_add(Vector4F left, Vector4F right)
    {
        Vector4F result;
        result.x = left.x + right.x;
        result.y = left.y + right.y;
        result.z = left.z + right.z;
        result.w = left.w + right.w;
        return result;
    }

    Vector4F vector4f_pointer_add(Vector4F* left, Vector4F* right)
    {
        return vector4f_add(*left, *right);
    }

    Vector4F Vector4F_add_3(Vector4F v1, Vector4F v2, Vector4F v3)
    {
        Vector4F result;
        result.x = v1.x + v2.x + v3.x;
        result.y = v1.y + v2.y + v3.y;
        result.z = v1.z + v2.z + v3.z;
        result.w = v1.w + v2.w + v3.w;
        return result;
    }

    Vector4F Vector4F_add_4(Vector4F v1, Vector4F v2, Vector4F v3, Vector4F v4)
    {
        Vector4F result;
        result.x = v1.x + v2.x + v3.x + v4.x;
        result.y = v1.y + v2.y + v3.y + v4.y;
        result.z = v1.z + v2.z + v3.z + v4.z;
        result.w = v1.w + v2.w + v3.w + v4.w;
        return result;
    }

    Vector4F Vector4F_add_5(Vector4F v1, Vector4F v2, Vector4F v3, Vector4F v4, Vector4F v5)
    {
        Vector4F result;
        result.x = v1.x + v2.x + v3.x + v4.x + v5.x;
        result.y = v1.y + v2.y + v3.y + v4.y + v5.y;
        result.z = v1.z + v2.z + v3.z + v4.z + v5.z;
        result.w = v1.w + v2.w + v3.w + v4.w + v5.w;
        return result;
    }

    Vector4F vector4f_negate(Vector4F value)
    {
        Vector4F result;
        result.x = -value.x;
        result.y = -value.y;
        result.z = -value.z;
        result.w = -value.w;
        return result;
    }
}