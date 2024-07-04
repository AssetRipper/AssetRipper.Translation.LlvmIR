extern "C" {
    char get_hello_world_character(int index)
    {
        const char* data = "Hello world!";
        return index < 0 || index >= 12 ? '\0' : data[index];
    }
}