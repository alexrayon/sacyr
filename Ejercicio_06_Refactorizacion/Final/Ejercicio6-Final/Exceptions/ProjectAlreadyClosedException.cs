using System;

namespace Ejercicio6_Final.Exceptions
{
    public class ProjectAlreadyClosedException : InvalidOperationException
    {
        public ProjectAlreadyClosedException(int projectId)
            : base($"El proyecto con id {projectId} ya esta cerrado.")
        {
        }
    }
}
