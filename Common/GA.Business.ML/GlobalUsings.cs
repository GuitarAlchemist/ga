global using System;
global using System.Collections.Generic;
global using System.Collections.Concurrent;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Text;
global using System.Text.Json;
global using System.IO;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Buffers;
global using System.Runtime.InteropServices;

global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using JetBrains.Annotations;

global using GA.Business.ML.Abstractions;
global using GA.Business.ML.Configuration;
global using GA.Business.ML.Embeddings;
global using GA.Business.ML.Embeddings.Services;
global using GA.Business.ML.Musical.Explanation;
global using GA.Business.ML.Text.Internal;

// Dependencies from GA.Business.Core
global using GA.Business.Core.Abstractions;
global using GA.Business.Core.Fretboard.Voicings.Search;

