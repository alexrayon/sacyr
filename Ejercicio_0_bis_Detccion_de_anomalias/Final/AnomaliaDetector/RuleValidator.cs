using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AnomaliaDetector
{
    public class RuleValidator
    {
        private HashSet<string> idsVistos = new HashSet<string>();

        public List<Anomalia> ValidarRegistro(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();

            // Validar ID_Obra
            anomalias.AddRange(ValidarIDObra(registro));

            // Validar Nombre_Obra
            anomalias.AddRange(ValidarNombreObra(registro));

            // Validar Fecha_Inicio
            anomalias.AddRange(ValidarFechaInicio(registro));

            // Validar Fecha_Fin
            anomalias.AddRange(ValidarFechaFin(registro));

            // Validar Presupuesto
            anomalias.AddRange(ValidarPresupuesto(registro));

            // Validar Estado
            anomalias.AddRange(ValidarEstado(registro));

            // Validar reglas transversales
            anomalias.AddRange(ValidarReglasTransversales(registro));

            // Verificar duplicados (global)
            if (!idsVistos.Add(registro.IdObra))
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A004", "Duplicado: ID_Obra repetido en el archivo."));
            }

            return anomalias;
        }

        private List<Anomalia> ValidarIDObra(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            if (string.IsNullOrEmpty(registro.IdObra))
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A003", "Campo Obligatorio Vacío: ID_Obra."));
            }
            else if (!Regex.IsMatch(registro.IdObra, @"^[A-Za-z0-9]{3,10}$"))
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A007", "Caracteres Inválidos: ID_Obra debe contener solo alfanuméricos, longitud 3-10."));
            }
            return anomalias;
        }

        private List<Anomalia> ValidarNombreObra(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            if (string.IsNullOrEmpty(registro.NombreObra))
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A003", "Campo Obligatorio Vacío: Nombre_Obra."));
            }
            else if (registro.NombreObra.Length < 5 || registro.NombreObra.Length > 100)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A006", "Longitud Excedida: Nombre_Obra debe tener 5-100 caracteres."));
            }
            return anomalias;
        }

        private List<Anomalia> ValidarFechaInicio(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            if (!registro.FechaInicio.HasValue)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A001", "Formato de Campo Inválido: Fecha_Inicio no tiene formato DD/MM/YYYY válido."));
            }
            else if (registro.FechaInicio.Value < new DateTime(2000, 1, 1) || registro.FechaInicio.Value > DateTime.Now)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A002", "Valor Fuera de Rango: Fecha_Inicio fuera del rango permitido."));
            }
            return anomalias;
        }

        private List<Anomalia> ValidarFechaFin(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            if (registro.FechaFin.HasValue)
            {
                if (registro.FechaFin.Value < new DateTime(2000, 1, 1) || registro.FechaFin.Value > DateTime.Now.AddYears(5))
                {
                    anomalias.Add(new Anomalia(registro.NumeroLinea, "A002", "Valor Fuera de Rango: Fecha_Fin fuera del rango permitido."));
                }
            }
            return anomalias;
        }

        private List<Anomalia> ValidarPresupuesto(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            if (!registro.Presupuesto.HasValue)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A003", "Campo Obligatorio Vacío: Presupuesto."));
            }
            else if (registro.Presupuesto.Value <= 0 || registro.Presupuesto.Value > 100000000)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A002", "Valor Fuera de Rango: Presupuesto debe ser positivo y <= 100.000.000."));
            }
            return anomalias;
        }

        private List<Anomalia> ValidarEstado(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            var estadosValidos = new[] { "Planificada", "En_Progreso", "Completada", "Cancelada" };
            if (string.IsNullOrEmpty(registro.Estado))
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A003", "Campo Obligatorio Vacío: Estado."));
            }
            else if (!estadosValidos.Contains(registro.Estado))
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A001", "Formato de Campo Inválido: Estado no válido."));
            }
            return anomalias;
        }

        private List<Anomalia> ValidarReglasTransversales(RegistroObra registro)
        {
            var anomalias = new List<Anomalia>();
            if (registro.Estado == "Completada" && !registro.FechaFin.HasValue)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A005", "Inconsistencia Lógica: Estado 'Completada' requiere Fecha_Fin."));
            }
            if (registro.FechaInicio.HasValue && registro.FechaFin.HasValue && registro.FechaFin.Value < registro.FechaInicio.Value)
            {
                anomalias.Add(new Anomalia(registro.NumeroLinea, "A005", "Inconsistencia Lógica: Fecha_Fin anterior a Fecha_Inicio."));
            }
            return anomalias;
        }
    }
}