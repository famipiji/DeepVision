using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace iVault.Api.DTOs
{
    public class RecordUploadDto
    {
        public Guid RecordDefinitionId { get; set; }

        // Dynamic metadata from the React form
        //[FromForm(Name = "Metadata")]
        //public Dictionary<string, string>? Metadata { get; set; } = new();

        [FromForm(Name = "metadata")]
        public string? Metadata { get; set; }

        // The actual document file for SeaweedFS
        [FromForm(Name = "file")]
        public IFormFile? File { get; set; }
    }
}