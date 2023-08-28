    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoAlbum
{
    class ItemsToSave
    {
        public string FolderPath { get; set; }

        public Dictionary<string, List<int>> VirtualDirectories { get; set; }
    }
}
