template<typename T>
T add(T a, T b) {
    return a + b;
}

extern "C" {
    int addInt(int a, int b) {
        return add<int>(a, b);
    }

    double addDouble(double a, double b) {
        return add<double>(a, b);
    }
}