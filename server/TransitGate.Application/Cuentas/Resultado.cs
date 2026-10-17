namespace TransitGate.Application.Cuentas;

/// <summary>Cómo responde la API a cada error: 400, 404 (también si es de otro cliente, RF-06 CA1) o 409.</summary>
public enum TipoError
{
    Invalido,
    NoEncontrado,
    Conflicto,
}

/// <summary>Código estable más mensaje en español claro, como en el Contrato §1 (RNF-32).</summary>
public sealed record ErrorCuenta(TipoError Tipo, string Codigo, string Mensaje)
{
    public static ErrorCuenta Invalido(string codigo, string mensaje) => new(TipoError.Invalido, codigo, mensaje);

    public static ErrorCuenta Conflicto(string codigo, string mensaje) => new(TipoError.Conflicto, codigo, mensaje);

    public static readonly ErrorCuenta TarjetaNoEncontrada =
        new(TipoError.NoEncontrado, "TarjetaNoEncontrada", "No encontramos tu tarjeta.");
}

/// <summary>El valor de un caso de uso del módulo de cuentas, o el error por el que no se hizo nada.</summary>
public sealed record Resultado<T>(T? Valor, ErrorCuenta? Error)
{
    public bool Exito => Error is null;

    public static implicit operator Resultado<T>(T valor) => new(valor, null);

    public static implicit operator Resultado<T>(ErrorCuenta error) => new(default, error);
}
