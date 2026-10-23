namespace Agojie.Core
{
    /// <summary>
    /// Implémenté par ce que les ennemis peuvent repérer (le joueur).
    /// Visibility : 1 = normal, &lt; 1 = discret (accroupi), &gt; 1 = voyant (course).
    /// </summary>
    public interface IStealthTarget
    {
        float Visibility { get; }
    }
}
