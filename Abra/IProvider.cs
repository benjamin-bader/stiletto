namespace Abra
{
    /// <summary>
    /// Represents an object which can provide a dependency of type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of dependency which can be provided.
    /// </typeparam>
    /// <remarks>
    /// A property or constructor parameter may be declared an
    /// <see cref="IProvider&lt;T&gt;"/> in order to, for example, break up
    /// a circular dependency.
    /// More-than-rare use of this mechanism is frequently a code smell.
    /// </remarks>
    public interface IProvider<out T>
    {
        /// <summary>
        /// Gets an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <returns>
        /// Returns an instance of type <typeparamref name="T"/>.
        /// </returns>
        T Get();
    }
}
