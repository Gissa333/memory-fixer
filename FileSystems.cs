using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MemoryFixer
{
    // ============================================================
    //   محلل NTFS MFT — لاستعادة أسماء الملفات الأصلية
    // ============================================================
    public class MftReader
    {
        public class MftEntry
        {
            public ulong RecordNumber;
            public bool InUse;
            public bool IsDirectory;
            public string FileName;
            public ulong ParentRecord;
            public long RealSize;
            public List<Tuple<long, long>> DataRuns = new List<Tuple<long, long>>();
        }

        public static List<MftEntry> ReadDeletedEntries(string drivePath, Action<int, string> progress)
        {
            var result = new List<MftEntry>();
            try
            {
                using (var stream = new FileStream(drivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                {
                    byte[] boot = new byte[512];
                    stream.Read(boot, 0, 512);

                    string oem = Encoding.ASCII.GetString(boot, 3, 8);
                    if (!oem.StartsWith("NTFS")) return result;

                    int bytesPerSector = BitConverter.ToInt16(boot, 11);
                    int sectorsPerCluster = boot[13];
                    int clusterSize = bytesPerSector * sectorsPerCluster;
                    if (clusterSize <= 0) clusterSize = 4096;

                    long mftCluster = BitConverter.ToInt64(boot, 0x30);
                    long mftOffset = mftCluster * clusterSize;
                    if (mftOffset <= 0 || mftOffset >= stream.Length) return result;

                    int bytesPerRecord = 1024;
                    long pos = mftOffset;
                    long recordNum = 0;
                    int maxRecords = 2000000;

                    while (recordNum < maxRecords && pos + bytesPerRecord <= stream.Length)
                    {
                        stream.Seek(pos, SeekOrigin.Begin);
                        byte[] record = new byte[bytesPerRecord];
                        int read = stream.Read(record, 0, bytesPerRecord);
                        if (read < bytesPerRecord) break;

                        if (record[0] != 'F' || record[1] != 'I' || record[2] != 'L' || record[3] != 'E')
                        {
                            if (recordNum > 50) break;
                            pos += bytesPerRecord;
                            recordNum++;
                            continue;
                        }

                        try
                        {
                            var entry = ParseRecord(record, (ulong)recordNum, clusterSize);
                            if (entry != null && !entry.InUse && !entry.IsDirectory &&
                                !string.IsNullOrEmpty(entry.FileName) && entry.RealSize > 0)
                            {
                                result.Add(entry);
                            }
                        }
                        catch { }

                        pos += bytesPerRecord;
                        recordNum++;

                        if (recordNum % 5000 == 0 && progress != null)
                        {
                            progress(0, $"تم فحص {recordNum:N0} سجل من MFT... (وجدت {result.Count} ملف محذوف)");
                        }
                    }

                    if (progress != null)
                        progress(100, $"انتهى فحص MFT. عُثر على {result.Count} ملف محذوف.");
                }
            }
            catch (Exception ex)
            {
                if (progress != null) progress(0, "خطأ في قراءة MFT: " + ex.Message);
            }
            return result;
        }

        private static MftEntry ParseRecord(byte[] record, ulong recordNum, int clusterSize)
        {
            var entry = new MftEntry { RecordNumber = recordNum };

            ushort flags = BitConverter.ToUInt16(record, 22);
            entry.InUse = (flags & 0x01) != 0;
            entry.IsDirectory = (flags & 0x02) != 0;

            int attrOffset = BitConverter.ToUInt16(record, 20);
            if (attrOffset < 24 || attrOffset >= record.Length) return null;

            while (attrOffset + 8 < record.Length)
            {
                uint attrType = BitConverter.ToUInt32(record, attrOffset);
                if (attrType == 0xFFFFFFFF) break;

                uint attrLen = BitConverter.ToUInt32(record, attrOffset + 4);
                if (attrLen == 0 || attrLen > record.Length || attrOffset + attrLen > record.Length) break;

                byte nonResident = record[attrOffset + 8];

                if (attrType == 0x30)
                {
                    int contentLen = BitConverter.ToInt32(record, attrOffset + 16);
                    int contentOff = BitConverter.ToUInt16(record, attrOffset + 20);
                    int absOff = attrOffset + contentOff;
                    if (contentLen >= 66 && absOff + 66 < record.Length)
                    {
                        entry.ParentRecord = BitConverter.ToUInt64(record, absOff) & 0x0000FFFFFFFFFFFF;
                        entry.RealSize = BitConverter.ToInt64(record, absOff + 48);
                        int nameLen = record[absOff + 64];
                        if (nameLen > 0 && absOff + 66 + nameLen * 2 <= record.Length)
                        {
                            entry.FileName = Encoding.Unicode.GetString(record, absOff + 66, nameLen * 2);
                        }
                    }
                }
                else if (attrType == 0x80 && nonResident == 1)
                {
                    int runOff = BitConverter.ToUInt16(record, attrOffset + 32);
                    int runStart = attrOffset + runOff;
                    int p = runStart;
                    long currentLcn = 0;

                    while (p < record.Length && p < attrOffset + attrLen)
                    {
                        byte header = record[p];
                        if (header == 0) break;

                        int lenBytes = header & 0x0F;
                        int offBytes = (header >> 4) & 0x0F;
                        p++;

                        if (lenBytes == 0 || p + lenBytes + offBytes > record.Length) break;

                        long runLen = 0;
                        for (int k = 0; k < lenBytes; k++)
                            runLen |= (long)record[p + k] << (8 * k);
                        p += lenBytes;

                        long runDelta = 0;
                        if (offBytes > 0)
                        {
                            for (int k = 0; k < offBytes; k++)
                                runDelta |= (long)record[p + k] << (8 * k);
                            if ((record[p + offBytes - 1] & 0x80) != 0)
                                runDelta |= -1L << (8 * offBytes);
                            p += offBytes;
                        }

                        currentLcn += runDelta;
                        if (runLen > 0 && currentLcn > 0)
                        {
                            entry.DataRuns.Add(Tuple.Create(currentLcn * clusterSize, runLen * clusterSize));
                        }
                    }
                }

                attrOffset += (int)attrLen;
            }

            return entry;
        }
    }

    // ============================================================
    //   محلل FAT32 / exFAT — لقراءة أسماء الملفات الأصلية
    // ============================================================
    public class FatReader
    {
        public class FatEntry
        {
            public string FileName;
            public long Size;
            public long FirstCluster;
            public bool IsDirectory;
            public bool Deleted;
        }

        public static List<FatEntry> ReadDirectory(string drivePath)
        {
            var result = new List<FatEntry>();
            try
            {
                using (var stream = new FileStream(drivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096))
                {
                    byte[] boot = new byte[512];
                    stream.Read(boot, 0, 512);

                    int bytesPerSector = BitConverter.ToInt16(boot, 11);
                    int sectorsPerCluster = boot[13];
                    int reservedSectors = BitConverter.ToInt16(boot, 14);
                    int numFats = boot[16];
                    long fatSizeSectors = BitConverter.ToUInt32(boot, 36);
                    long rootCluster = BitConverter.ToUInt32(boot, 44);
                    int clusterSize = bytesPerSector * sectorsPerCluster;

                    if (bytesPerSector != 512 || sectorsPerCluster == 0) return result;
                    if (clusterSize <= 0) clusterSize = 4096;

                    string fsType = Encoding.ASCII.GetString(boot, 82, 8).Trim();
                    bool isFat32 = fsType.StartsWith("FAT32");

                    long dataStart;
                    long rootOffset;

                    if (isFat32)
                    {
                        dataStart = (reservedSectors + numFats * fatSizeSectors) * (long)bytesPerSector;
                        rootOffset = dataStart + (rootCluster - 2) * (long)clusterSize;
                    }
                    else
                    {
                        dataStart = reservedSectors * (long)bytesPerSector;
                        rootOffset = dataStart;
                    }

                    ReadDirEntries(stream, rootOffset, clusterSize, result);
                }
            }
            catch { }
            return result;
        }

        private static void ReadDirEntries(FileStream stream, long offset, int clusterSize, List<FatEntry> result)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] buffer = new byte[clusterSize * 4];
            int read = stream.Read(buffer, 0, buffer.Length);

            var lfnParts = new List<string>();

            for (int i = 0; i + 32 <= read; i += 32)
            {
                byte firstByte = buffer[i];
                if (firstByte == 0x00) break;

                if (firstByte == 0xE5)
                {
                    lfnParts.Clear();
                    continue;
                }

                byte attr = buffer[i + 11];

                if (attr == 0x0F)
                {
                    var chars = new char[13];
                    chars[0] = (char)BitConverter.ToUInt16(buffer, i + 1);
                    chars[1] = (char)BitConverter.ToUInt16(buffer, i + 3);
                    chars[2] = (char)BitConverter.ToUInt16(buffer, i + 5);
                    chars[3] = (char)BitConverter.ToUInt16(buffer, i + 7);
                    chars[4] = (char)BitConverter.ToUInt16(buffer, i + 9);
                    chars[5] = (char)BitConverter.ToUInt16(buffer, i + 14);
                    chars[6] = (char)BitConverter.ToUInt16(buffer, i + 16);
                    chars[7] = (char)BitConverter.ToUInt16(buffer, i + 18);
                    chars[8] = (char)BitConverter.ToUInt16(buffer, i + 20);
                    chars[9] = (char)BitConverter.ToUInt16(buffer, i + 22);
                    chars[10] = (char)BitConverter.ToUInt16(buffer, i + 24);
                    chars[11] = (char)BitConverter.ToUInt16(buffer, i + 28);
                    chars[12] = (char)BitConverter.ToUInt16(buffer, i + 30);

                    var part = new string(chars);
                    int nullIdx = part.IndexOf('\0');
                    if (nullIdx >= 0) part = part.Substring(0, nullIdx);

                    byte seq = buffer[i];
                    if (seq > 0 && part.Length > 0)
                        lfnParts.Insert(0, part);
                    continue;
                }

                string shortName = "";
                for (int k = 0; k < 8; k++)
                {
                    char c = (char)buffer[i + k];
                    if (c == ' ') break;
                    shortName += c;
                }
                string ext = "";
                for (int k = 8; k < 11; k++)
                {
                    char c = (char)buffer[i + k];
                    if (c == ' ') break;
                    ext += c;
                }
                if (ext.Length > 0) shortName += "." + ext;

                string fullName = lfnParts.Count > 0 ? string.Concat(lfnParts) : shortName;
                lfnParts.Clear();

                if (string.IsNullOrWhiteSpace(fullName)) continue;
                if (fullName == "." || fullName == "..") continue;

                long size = BitConverter.ToUInt32(buffer, i + 28);
                long cluster = BitConverter.ToUInt32(buffer, i + 26);

                result.Add(new FatEntry
                {
                    FileName = fullName,
                    Size = size,
                    FirstCluster = cluster,
                    Deleted = false,
                    IsDirectory = (attr & 0x10) != 0
                });
            }
        }
    }

    // ============================================================
    //   عارض Hex — لعرض أول 512 بايت من أي ملف
    // ============================================================
    public class HexViewer : Control
    {
        private byte[] _data;
        private string _title = "";

        public HexViewer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = new Font("Consolas", 8.5F);
            BackColor = Color.FromArgb(12, 12, 20);
            ForeColor = Color.FromArgb(235, 235, 245);
        }

        public void SetData(byte[] data, string title)
        {
            _data = data;
            _title = title ?? "";
            Invalidate();
        }

        public void Clear()
        {
            _data = null;
            _title = "";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            using (var brush = new SolidBrush(ForeColor))
            using (var brushDim = new SolidBrush(Color.FromArgb(140, 140, 175)))
            using (var brushOffset = new SolidBrush(Color.FromArgb(120, 170, 255)))
            {
                if (_data == null || _data.Length == 0)
                {
                    e.Graphics.DrawString("اختر ملفًا لعرض محتواه هنا.", Font, brushDim, 10, 10);
                    return;
                }

                int y = 5;
                if (!string.IsNullOrEmpty(_title))
                {
                    using (var boldFont = new Font(Font, FontStyle.Bold))
                        e.Graphics.DrawString(_title, boldFont, brushOffset, 5, y);
                    y += 20;
                }

                int max = Math.Min(_data.Length, 512);
                int bytesPerLine = 16;

                for (int i = 0; i < max; i += bytesPerLine)
                {
                    if (y + 15 > Height) break;

                    string offsetStr = i.ToString("X8");
                    e.Graphics.DrawString(offsetStr, Font, brushOffset, 5, y);

                    StringBuilder hex = new StringBuilder();
                    StringBuilder ascii = new StringBuilder();
                    for (int j = 0; j < bytesPerLine && i + j < max; j++)
                    {
                        byte b = _data[i + j];
                        hex.Append(b.ToString("X2"));
                        hex.Append(' ');
                        ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
                    }

                    e.Graphics.DrawString(hex.ToString(), Font, brush, 85, y);
                    e.Graphics.DrawString(ascii.ToString(), Font, brushDim, 425, y);
                    y += 15;
                }
            }
        }
    }
}
