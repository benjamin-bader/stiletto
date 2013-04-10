namespace Abra
{
    public interface IProvider<out T>
    {
        T Get();
    }
}
