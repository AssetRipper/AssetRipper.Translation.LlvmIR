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

    Vector2F vector2f_add_3(Vector2F v1, Vector2F v2, Vector2F v3)
    {
        Vector2F result;
        result.x = v1.x + v2.x + v3.x;
        result.y = v1.y + v2.y + v3.y;
        return result;
    }

    Vector2F vector2f_add_4(Vector2F v1, Vector2F v2, Vector2F v3, Vector2F v4)
    {
        Vector2F result;
        result.x = v1.x + v2.x + v3.x + v4.x;
        result.y = v1.y + v2.y + v3.y + v4.y;
        return result;
    }

    Vector2F vector2f_add_5(Vector2F v1, Vector2F v2, Vector2F v3, Vector2F v4, Vector2F v5)
    {
        Vector2F result;
        result.x = v1.x + v2.x + v3.x + v4.x + v5.x;
        result.y = v1.y + v2.y + v3.y + v4.y + v5.y;
        return result;
    }
}