namespace AndroidDebloater.Core.Models;

/// <summary>Herkunft eines Pakets: fest im System oder vom Benutzer installiert.</summary>
public enum PackageKind
{
    /// <summary>Teil des Systemabbilds (z. B. /system, /product, /system_ext, /vendor).</summary>
    System,

    /// <summary>Vom Benutzer bzw. über einen Store installiert (CodePath unter /data/app).</summary>
    User,
}
