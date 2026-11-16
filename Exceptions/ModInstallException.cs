using System;

namespace KMRLauncherMvvm.Exceptions;

public class ModInstallException(string message) : Exception(message);