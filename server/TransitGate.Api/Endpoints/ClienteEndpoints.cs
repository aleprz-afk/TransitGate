using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TransitGate.Api.Contratos;
using TransitGate.Api.Seguridad;
using TransitGate.Application;
using TransitGate.Application.Cuentas;
using TransitGate.Infrastructure.Persistencia;

namespace TransitGate.Api.Endpoints;

/// <summary>
/// Sesión (Contrato §2) y portal del cliente (§3, §4, §5 y §9). Las rutas de "mi tarjeta" solo ven la tarjeta
/// del cliente de la sesión: el id sale de la cookie, nunca de la URL (RF-06 CA1).
/// </summary>
public static partial class ClienteEndpoints
{
    public static IEndpointRouteBuilder MapClientes(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Sesión");
        auth.MapPost("/login", IniciarSesion).WithSummary("Inicia sesión con correo y contraseña (RF-02); 6 intentos fallidos bloquean 5 min (RNF-22)");
        auth.MapPost("/logout", CerrarSesion).WithSummary("Cierra la sesión (RF-03)");
        auth.MapGet("/sesion", SesionActual).RequireAuthorization()
            .WithSummary("Rol y nombre de la sesión actual, para que la web sepa a dónde llevar al usuario al recargar");

        var clientes = app.MapGroup("/api/clientes").WithTags("Clientes");
        clientes.MapPost("", Registrar).WithSummary("Registro del cliente: crea su tarjeta con saldo 0 e inicia sesión (RF-01, RF-07)");
        clientes.MapGet("/me", MiPerfil).RequireAuthorization(Politicas.Cliente).WithSummary("Datos del cliente de la sesión");
        clientes.MapPatch("/me", CambiarPerfil).RequireAuthorization(Politicas.Cliente).WithSummary("Cambia el teléfono (RF-05)");

        var tarjeta = app.MapGroup("/api/tarjetas/mi-tarjeta").WithTags("Mi tarjeta").RequireAuthorization(Politicas.Cliente);
        tarjeta.MapGet("", MiTarjeta).WithSummary("Saldo y estado del soporte (RF-16)");
        tarjeta.MapPost("/solicitud-fisica", SolicitarFisica).WithSummary("Pide la tarjeta física (RF-08)");
        tarjeta.MapPost("/confirmar-recepcion", ConfirmarRecepcion).WithSummary("Confirma que recibió la tarjeta física: Entregada → Activa (RF-11)");
        tarjeta.MapPost("/recargas", Recargar).WithSummary("Recarga con pago simulado; Idempotency-Key evita el doble cobro (RF-15)");
        tarjeta.MapGet("/recargas", MisRecargas).WithSummary("Historial de recargas (RF-17)");
        tarjeta.MapGet("/viajes", MisViajes).WithSummary("Historial de viajes, últimos 100 (RF-39)");

        return app;
    }

    private static async Task<Results<Ok<RespuestaSesion>, BadRequest<ErrorApi>, UnauthorizedHttpResult, JsonHttpResult<ErrorApi>>> IniciarSesion(
        SolicitudLogin login, UserManager<Usuario> usuarios, SignInManager<Usuario> sesiones, IClienteRepositorio clientes)
    {
        if (string.IsNullOrWhiteSpace(login.Correo) || string.IsNullOrEmpty(login.Password))
            return TypedResults.BadRequest(new ErrorApi("LoginIncompleto", "Escribe tu correo y tu contraseña."));

        // RF-02 CA3: el mismo mensaje si falla el correo o la contraseña.
        var invalidas = TypedResults.Json(
            new ErrorApi("CredencialesInvalidas", "Correo o contraseña incorrectos."), statusCode: StatusCodes.Status401Unauthorized);

        var usuario = await usuarios.FindByEmailAsync(login.Correo.Trim().ToLowerInvariant());
        if (usuario is null || !usuario.Activo)   // RF-02 CA4
            return invalidas;

        var resultado = await sesiones.PasswordSignInAsync(usuario, login.Password, isPersistent: false, lockoutOnFailure: true);
        if (resultado.IsLockedOut)
            return TypedResults.Json(
                new ErrorApi("LoginBloqueado", "Demasiados intentos fallidos. Intenta de nuevo en 5 minutos."),
                statusCode: StatusCodes.Status401Unauthorized);
        if (!resultado.Succeeded)
            return invalidas;

        return TypedResults.Ok(await SesionDeAsync(usuario, usuarios, clientes));
    }

    private static async Task<NoContent> CerrarSesion(SignInManager<Usuario> sesiones)
    {
        await sesiones.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<RespuestaSesion>, UnauthorizedHttpResult>> SesionActual(
        ClaimsPrincipal usuario, UserManager<Usuario> usuarios, IClienteRepositorio clientes)
    {
        if (await usuarios.GetUserAsync(usuario) is not { Activo: true } cuenta)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(await SesionDeAsync(cuenta, usuarios, clientes));
    }

    private static async Task<Results<Created<RespuestaRegistro>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> Registrar(
        SolicitudRegistro registro, RegistrarCliente registrarCliente, UserManager<Usuario> usuarios, SignInManager<Usuario> sesiones)
    {
        var resultado = await registrarCliente.EjecutarAsync(new DatosRegistro(
            registro.NombreCompleto, registro.TipoDocumento, registro.NumeroDocumento, registro.Correo,
            registro.Telefono, registro.Password, registro.AutorizaTratamientoDatos));
        if (resultado.Error is { } error)
            return Error<Created<RespuestaRegistro>>(error);

        // RF-01 CA1: queda con sesión iniciada.
        var registrado = resultado.Valor!;
        await sesiones.SignInAsync((await usuarios.FindByIdAsync(registrado.ClienteId.ToString()))!, isPersistent: false);
        return TypedResults.Created(
            "/api/clientes/me",
            new RespuestaRegistro(IdsPublicos.Cliente(registrado.ClienteId), IdsPublicos.Tarjeta(registrado.TarjetaId)));
    }

    private static async Task<Results<Ok<PerfilCliente>, NotFound<ErrorApi>>> MiPerfil(
        ClaimsPrincipal usuario, IClienteRepositorio clientes, CancellationToken ct)
    {
        if (await clientes.ObtenerAsync(usuario.IdUsuario(), ct) is not { } cliente)
            return TypedResults.NotFound(new ErrorApi(ErrorCuenta.TarjetaNoEncontrada.Codigo, "No encontramos tus datos."));

        return TypedResults.Ok(new PerfilCliente(
            IdsPublicos.Cliente(cliente.Id), cliente.NombreCompleto, cliente.Correo, cliente.Telefono,
            cliente.TipoDocumento, cliente.NumeroDocumento));
    }

    private static async Task<Results<Ok, BadRequest<ErrorApi>, NotFound<ErrorApi>>> CambiarPerfil(
        CambioPerfil cambio, ClaimsPrincipal usuario, IClienteRepositorio clientes, IUnidadDeTrabajo unidadDeTrabajo, CancellationToken ct)
    {
        if (cambio.Telefono is null || !Celular().IsMatch(cambio.Telefono.Trim()))
            return TypedResults.BadRequest(new ErrorApi("TelefonoInvalido", "El teléfono debe tener 10 dígitos."));
        if (await clientes.ObtenerAsync(usuario.IdUsuario(), ct) is not { } cliente)
            return TypedResults.NotFound(new ErrorApi(ErrorCuenta.TarjetaNoEncontrada.Codigo, "No encontramos tus datos."));

        cliente.CambiarTelefono(cambio.Telefono.Trim());
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<MiTarjetaJson>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> MiTarjeta(
        ClaimsPrincipal usuario, ConsultarMiTarjeta consultar, CancellationToken ct) =>
        Responder(await consultar.EjecutarAsync(usuario.IdUsuario(), ct), MiTarjetaJson.Desde);

    private static async Task<Results<Created<RespuestaSolicitudFisica>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> SolicitarFisica(
        ClaimsPrincipal usuario, SolicitarTarjetaFisica solicitar, CancellationToken ct)
    {
        var resultado = await solicitar.EjecutarAsync(usuario.IdUsuario(), ct);
        if (resultado.Error is { } error)
            return Error<Created<RespuestaSolicitudFisica>>(error);

        return TypedResults.Created(
            "/api/tarjetas/mi-tarjeta",
            new RespuestaSolicitudFisica(IdsPublicos.Solicitud(resultado.Valor!.SolicitudId), resultado.Valor.Estado.ToString()));
    }

    private static async Task<Results<Ok<RespuestaEstadoTarjeta>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> ConfirmarRecepcion(
        ClaimsPrincipal usuario, ConfirmarRecepcion confirmar, CancellationToken ct) =>
        Responder(await confirmar.EjecutarAsync(usuario.IdUsuario(), ct), estado => new RespuestaEstadoTarjeta(estado.ToString()));

    private static async Task<Results<Created<RecargaJson>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> Recargar(
        SolicitudRecarga recarga, [FromHeader(Name = "Idempotency-Key")] string? claveIdempotencia,
        ClaimsPrincipal usuario, Recargar recargar, CancellationToken ct)
    {
        if (recarga.Monto is not { } monto)
            return TypedResults.BadRequest(new ErrorApi("MontoRequerido", "Falta el monto."));
        if (claveIdempotencia is { Length: > 64 })
            return TypedResults.BadRequest(new ErrorApi("ClaveIdempotenciaInvalida", "Idempotency-Key admite hasta 64 caracteres."));

        var resultado = await recargar.EjecutarAsync(usuario.IdUsuario(), monto, claveIdempotencia, recarga.SimularRechazo, ct);
        if (resultado.Error is { } error)
            return Error<Created<RecargaJson>>(error);

        return TypedResults.Created("/api/tarjetas/mi-tarjeta/recargas", RecargaJson.Desde(resultado.Valor!));
    }

    private static async Task<Results<Ok<RecargaJson[]>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> MisRecargas(
        ClaimsPrincipal usuario, ConsultarRecargas consultar, CancellationToken ct) =>
        Responder(await consultar.EjecutarAsync(usuario.IdUsuario(), ct), recargas => recargas.Select(RecargaJson.Desde).ToArray());

    private static async Task<Results<Ok<ViajeJson[]>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>>> MisViajes(
        ClaimsPrincipal usuario, ConsultarViajes consultar, CancellationToken ct) =>
        Responder(await consultar.EjecutarAsync(usuario.IdUsuario(), ct), viajes => viajes.Select(ViajeJson.Desde).ToArray());

    private static async Task<RespuestaSesion> SesionDeAsync(Usuario usuario, UserManager<Usuario> usuarios, IClienteRepositorio clientes)
    {
        var roles = await usuarios.GetRolesAsync(usuario);
        var rol = Roles.Todos.FirstOrDefault(roles.Contains) ?? "";

        // El nombre del cliente vive en Clientes, no en la cuenta de acceso.
        var nombre = rol == Roles.Cliente && await clientes.ObtenerAsync(usuario.Id) is { } cliente
            ? cliente.NombreCompleto
            : usuario.NombreCompleto;
        return new RespuestaSesion(rol, nombre);
    }

    private static Results<Ok<T>, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>> Responder<TValor, T>(
        Resultado<TValor> resultado, Func<TValor, T> convertir) =>
        resultado.Error is { } error ? Error<Ok<T>>(error) : TypedResults.Ok(convertir(resultado.Valor!));

    // Un error del módulo de cuentas como 400, 404 (también si es de otro cliente, RF-06 CA1) o 409.
    private static Results<TExito, BadRequest<ErrorApi>, NotFound<ErrorApi>, Conflict<ErrorApi>> Error<TExito>(ErrorCuenta error)
        where TExito : IResult
    {
        var cuerpo = new ErrorApi(error.Codigo, error.Mensaje);
        return error.Tipo switch
        {
            TipoError.NoEncontrado => TypedResults.NotFound(cuerpo),
            TipoError.Conflicto => TypedResults.Conflict(cuerpo),
            _ => TypedResults.BadRequest(cuerpo),
        };
    }

    [GeneratedRegex("^[0-9]{10}$")]
    private static partial Regex Celular();
}
