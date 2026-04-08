using System;

namespace Ejercicio6_Final.Exceptions
{
    public class ProjectNotFoundException : InvalidOperationException
    {
        public ProjectNotFoundException(int projectId)
            : base($"No existe el proyecto con id {projectId}.")
        {
        }
    }
}
