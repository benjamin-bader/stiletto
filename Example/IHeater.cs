namespace Example
{
    interface IHeater
    {
        bool IsHot { get; }
        void On();
        void Off();
    }
}
