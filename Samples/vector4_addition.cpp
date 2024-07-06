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
}