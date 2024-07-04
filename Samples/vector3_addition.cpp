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
}