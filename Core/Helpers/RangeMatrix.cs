using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Helpers
{
    public class RangeMatrix
    {
        public int MinRow { get; set; }
        public int MaxRow { get; set; }
        public int MinCol { get; set; }
        public int MaxCol { get; set; }

        public int RowCount => MaxRow - MinRow + 1;
        public int ColumnCount => MaxCol - MinCol + 1;

        private HashSet<(int row, int col)> activeGrids = new();

        public static RangeMatrix? Create(RangeData? data)
        {
            if (data == null || data.Grids == null || data.Grids.Count == 0)
                return null;

            var matrix = new RangeMatrix();

            int minR = 0, maxR = 0, minC = 0, maxC = 0;

            foreach (var cell in data.Grids)
            {
                if (cell == null) continue;
                matrix.activeGrids.Add((cell.Row, cell.Col));
                if (cell.Row < minR) minR = cell.Row;
                if (cell.Row > maxR) maxR = cell.Row;
                if (cell.Col < minC) minC = cell.Col;
                if (cell.Col > maxC) maxC = cell.Col;
            }

            matrix.MinRow = minR;
            matrix.MaxRow = maxR;
            matrix.MinCol = minC;
            matrix.MaxCol = maxC;

            return matrix;
        }

        public string GetCellType(int gridRowIndex, int gridColIndex)
        {
            int actualRow = MinRow + gridRowIndex;
            int actualCol = MinCol + gridColIndex;

            if (actualRow == 0 && actualCol == 0)
                return "origin";

            if (activeGrids.Contains((actualRow, actualCol)))
                return "active";

            return "empty";
        }

        public int GetRowCoord(int gridRowIndex) => MinRow + gridRowIndex;
        public int GetColCoord(int gridColIndex) => MinCol + gridColIndex;
    }
}
