using System.Collections.Concurrent;
using TransitGate.Application.Validacion;

namespace TransitGate.Application.Taquilla;

/// <summary>
/// Las últimas respuestas a lecturas en modo registro, por torniquete. Una lectura en modo registro no deja
/// IntentoAcceso (RF-23 CA3), así que el reintento con el mismo lecturaId (RF-18 CA4) se contesta desde aquí:
/// sin esto, el reintento de una tarjeta ya ligada se validaría como un paso. El ESP32 reintenta a los pocos
/// segundos, así que basta guardar las últimas en memoria. Se usa con el cerrojo del torniquete tomado.
/// </summary>
public sealed class RespuestasDeRegistro
{
    private const int PorTorniquete = 10;

    private readonly ConcurrentDictionary<string, Queue<(string LecturaId, ResultadoLectura Resultado)>> _recientes = new();

    public ResultadoLectura? Buscar(string torniqueteId, string lecturaId) =>
        _recientes.TryGetValue(torniqueteId, out var cola)
            ? cola.FirstOrDefault(r => r.LecturaId == lecturaId).Resultado
            : null;

    public void Guardar(string torniqueteId, string lecturaId, ResultadoLectura resultado)
    {
        var cola = _recientes.GetOrAdd(torniqueteId, _ => new Queue<(string, ResultadoLectura)>());
        cola.Enqueue((lecturaId, resultado));
        if (cola.Count > PorTorniquete)
            cola.Dequeue();
    }
}
