using System;
using System.Collections.Generic;
using System.IO;

namespace AnomaliaDetector
{
    public class FileReader
    {
        public List<string> LeerArchivo(string rutaArchivo)
        {
            if (!File.Exists(rutaArchivo))
            {
                throw new FileNotFoundException("El archivo especificado no existe.", rutaArchivo);
            }

            var lineas = new List<string>();
            using (var reader = new StreamReader(rutaArchivo))
            {
                string linea;
                int numeroLinea = 0;
                while ((linea = reader.ReadLine()) != null)
                {
                    numeroLinea++;
                    if (numeroLinea == 1)
                    {
                        // Validar encabezado
                        if (linea != "ID_Obra,Nombre_Obra,Fecha_Inicio,Fecha_Fin,Presupuesto,Estado")
                        {
                            throw new FormatException("El encabezado del archivo no coincide con el formato esperado.");
                        }
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(linea))
                    {
                        lineas.Add(linea);
                    }
                }
            }
            return lineas;
        }
    }
}