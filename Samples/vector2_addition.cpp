typedef struct
{
    float x;
    float y;
} Vector2F;

extern "C" {
    Vector2F vector2f_add(Vector2F left, Vector2F right)
    {
        Vector2F result;
        result.x = left.x + right.x;
        result.y = left.y + right.y;
        return result;
    }

    Vector2F vector2f_pointer_add(Vector2F* left, Vector2F* right)
    {
        return vector2f_add(*left, *right);
    }
}