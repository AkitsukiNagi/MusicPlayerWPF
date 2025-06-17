using System;
using System.IO;
using TagLib;

namespace MusicPlayerWPF
{

    public class Metadata
    {
        public byte[] Thumbnail;
        public string Title = "None";
        public string Artists = "Unknown";
        public string Album;

        public Metadata(string title, string artists, string album)
        {
            Title = title;
            Artists = artists;
            Album = album;
        }

        public Metadata(string title, string artists, string album, byte[] thumbnail)
        {
            Title = title;
            Artists = artists;
            Album = album;
            Thumbnail = thumbnail;
        }
    }

    public class MetadataReader
    {
        public Metadata GetMetadataByFile(string filePath)
        {
            try
            {
                using (TagLib.File file = TagLib.File.Create(filePath))
                {
                    if (file == null) return null;
                    Metadata metadata = new Metadata(
                        !string.IsNullOrEmpty(file.Tag.Title) ? file.Tag.Title : Path.GetFileNameWithoutExtension(filePath),
                        file.Tag.Performers != null && file.Tag.Performers.Length > 0 ? string.Join("、", file.Tag.Performers) : null,
                        !string.IsNullOrEmpty(file.Tag.Album) ? file.Tag.Album : null);

                    if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                    {
                        IPicture picture = file.Tag.Pictures[0];

                        metadata.Thumbnail = picture.Data.Data;
                    }
                    return metadata;
                }
            }
            catch (UnsupportedFormatException ufe)
            {
                Console.Error.WriteLine($"Error: Unsupported format for {filePath}: {ufe.Message}");
            }
            catch (FileNotFoundException fnfe)
            {
                Console.Error.WriteLine($"Error: File {filePath} not found: {fnfe.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: An unexpected error occurred: {ex.Message}");
            }
            return null;
        }
    }
}
