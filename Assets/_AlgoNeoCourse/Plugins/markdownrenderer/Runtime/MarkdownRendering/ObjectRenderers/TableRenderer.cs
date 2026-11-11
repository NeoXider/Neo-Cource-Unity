using System.Collections.Generic;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMarkdownRenderer.ObjectRenderers
{
    /// <summary>
    /// Таблица Markdown (pipe table) как дерево UI Toolkit:
    /// <c>.md-table</c> → <c>.md-table-row</c> (строка шапки ещё и <c>.md-table-header</c>, каждая вторая
    /// строка тела — <c>.md-table-row--alt</c>) → <c>.md-table-cell</c>. Содержимое ячейки идёт через обычный
    /// конвейер рендера, поэтому выделение, встроенный код, ссылки и картинки внутри ячеек работают как в абзаце.
    /// <para>
    /// Ширина колонок не измеряется по тексту: каждая ячейка колонки получает один и тот же вес — длину самого
    /// длинного содержимого колонки в символах плюс <see cref="CellChromeWeight" />, зажатую в
    /// <see cref="MinColumnWeight" />…<see cref="MaxColumnWeight" />. Вес идёт в <c>flex-grow</c>, а
    /// <c>flex-basis</c> равен весу, умноженному на <see cref="ColumnUnitWidth" />, при одинаковом
    /// <c>flex-shrink</c>. Рост и сжатие при этом пропорциональны весу, поэтому колонки совпадают во всех
    /// строках и делят ширину родителя по весам, таблица не шире родителя, а длинный текст переносится.
    /// </para>
    /// <para>
    /// Почему не <c>flex-basis: 0</c>: при нём у таблицы нет собственной ширины, и в родителе, который
    /// обжимает содержимое (пузырь сообщения, <c>align-self: flex-start</c>), она схлопывалась до отступов
    /// ячеек. С базой по весу в таком родителе таблица получает оценку своей естественной ширины, а в
    /// родителе с заданной шириной доли колонок те же, что и при нулевой базе.
    /// </para>
    /// <para>
    /// Эти значения и <c>flex-direction: row</c> строки заданы инлайном — раскладка не зависит от подключённой
    /// темы; рамки, фон и отступы лежат в <c>Styles/MarkdownRenderer.uss</c>.
    /// </para>
    /// </summary>
    public class TableRenderer : MarkdownObjectRenderer<UIMarkdownRenderer, Table>
    {
        public const string TableClass = "md-table";
        public const string RowClass = "md-table-row";
        public const string HeaderRowClass = "md-table-header";
        public const string AlternateRowClass = "md-table-row--alt";
        public const string CellClass = "md-table-cell";
        public const string CellAlignLeftClass = "md-table-cell--left";
        public const string CellAlignCenterClass = "md-table-cell--center";
        public const string CellAlignRightClass = "md-table-cell--right";

        /// <summary>Нижняя граница веса: колонка из одного символа не схлопывается до своих отступов.</summary>
        public const float MinColumnWeight = 7f;

        /// <summary>Верхняя граница веса: одна длинная ячейка не отбирает ширину у остальных колонок.</summary>
        public const float MaxColumnWeight = 30f;

        /// <summary>
        /// Добавка к длине содержимого: отступы и рамка ячейки занимают примерно три символа независимо от
        /// текста. Без неё короткая колонка рядом с длинной получает ширину ровно под текст и переносит слово.
        /// </summary>
        public const float CellChromeWeight = 3f;

        /// <summary>
        /// Ширина единицы веса в пикселях для <c>flex-basis</c> — примерно один символ обычного текста. Задаёт
        /// только собственную (естественную) ширину таблицы там, где родитель её не ограничивает.
        /// </summary>
        public const float ColumnUnitWidth = 8f;

        // Картинка в ячейке текста не даёт; считаем её за короткое слово, чтобы колонка не стала минимальной.
        private const int ImageContentLength = 12;

        protected override void Write(UIMarkdownRenderer renderer, Table table)
        {
            List<TableRow> rows = new();
            int columnCount = 0;
            foreach (Block block in table)
            {
                if (block is TableRow row)
                {
                    rows.Add(row);
                    columnCount = Mathf.Max(columnCount, row.Count);
                }
            }

            if (rows.Count == 0 || columnCount == 0)
            {
                return;
            }

            if (renderer.LockTextCreation)
            {
                // Внутри списка весь текст пишется в одну метку и StartBlock возвращает null. Строим не дерево,
                // а читаемые строки «ячейка | ячейка» — содержимое таблицы не теряется.
                WriteAsText(renderer, rows);
                return;
            }

            float[] weights = ComputeColumnWeights(rows, columnCount);

            HtmlAttributes attribute = table.GetAttributes();
            List<string> classes = new() { TableClass };
            if (attribute.Classes != null)
            {
                classes.AddRange(attribute.Classes);
            }

            renderer.StartBlock(classes);

            int bodyRowIndex = 0;
            foreach (TableRow row in rows)
            {
                VisualElement rowElement = renderer.StartContainer(RowClass);
                rowElement.style.flexDirection = FlexDirection.Row;

                if (row.IsHeader)
                {
                    rowElement.AddToClassList(HeaderRowClass);
                }
                else
                {
                    if (bodyRowIndex % 2 == 1)
                    {
                        rowElement.AddToClassList(AlternateRowClass);
                    }

                    bodyRowIndex++;
                }

                // Строка короче шапки дополняется пустыми ячейками: иначе её колонки съезжают относительно остальных.
                for (int column = 0; column < columnCount; column++)
                {
                    VisualElement cellElement = renderer.StartContainer(CellClass);
                    cellElement.style.flexGrow = weights[column];
                    cellElement.style.flexShrink = 1f;
                    cellElement.style.flexBasis = weights[column] * ColumnUnitWidth;
                    cellElement.style.minWidth = 0f;

                    if (column < row.Count && row[column] is TableCell cell)
                    {
                        renderer.WriteChildren(cell);
                    }

                    ApplyCellLayout(cellElement, GetAlignment(table, column));
                    renderer.FinishBlock();
                }

                renderer.FinishBlock();
            }

            renderer.FinishBlock();
        }

        private static TableColumnAlign? GetAlignment(Table table, int column) =>
            column < table.ColumnDefinitions.Count ? table.ColumnDefinitions[column].Alignment : null;

        private static void ApplyCellLayout(VisualElement cellElement, TableColumnAlign? alignment)
        {
            TextAnchor? anchor = null;
            switch (alignment)
            {
                case TableColumnAlign.Left:
                    anchor = TextAnchor.UpperLeft;
                    cellElement.AddToClassList(CellAlignLeftClass);
                    break;
                case TableColumnAlign.Center:
                    anchor = TextAnchor.UpperCenter;
                    cellElement.AddToClassList(CellAlignCenterClass);
                    break;
                case TableColumnAlign.Right:
                    anchor = TextAnchor.UpperRight;
                    cellElement.AddToClassList(CellAlignRightClass);
                    break;
            }

            // Блок абзаца по умолчанию `flex-wrap: wrap`, и метка в нём сжимается до ширины своего текста —
            // выравнивать внутри неё нечего. Без переноса метка растягивается на всю ячейку.
            for (int i = 0; i < cellElement.childCount; i++)
            {
                cellElement[i].style.flexWrap = Wrap.NoWrap;
            }

            cellElement.Query<Label>().ForEach(label =>
            {
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.minWidth = 0f;
                if (anchor.HasValue)
                {
                    label.style.unityTextAlign = anchor.Value;
                }
            });
        }

        private static float[] ComputeColumnWeights(List<TableRow> rows, int columnCount)
        {
            float[] weights = new float[columnCount];
            for (int column = 0; column < columnCount; column++)
            {
                int longest = 0;
                foreach (TableRow row in rows)
                {
                    if (column < row.Count && row[column] is TableCell cell)
                    {
                        longest = Mathf.Max(longest, MeasureCell(cell));
                    }
                }

                weights[column] = Mathf.Clamp(longest + CellChromeWeight, MinColumnWeight, MaxColumnWeight);
            }

            return weights;
        }

        private static int MeasureCell(TableCell cell)
        {
            int length = 0;
            foreach (Block block in cell)
            {
                if (block is LeafBlock leaf && leaf.Inline != null)
                {
                    length += MeasureInlines(leaf.Inline.FirstChild);
                }
            }

            return length;
        }

        // Длина видимого текста: адреса ссылок и разметка в ширину колонки не идут.
        private static int MeasureInlines(Inline inline)
        {
            int length = 0;
            for (; inline != null; inline = inline.NextSibling)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        length += literal.Content.Length;
                        break;
                    case CodeInline code:
                        length += code.Content?.Length ?? 0;
                        break;
                    case LineBreakInline:
                        length++;
                        break;
                    case LinkInline { IsImage: true }:
                        length += ImageContentLength;
                        break;
                    case DelimiterInline delimiter:
                        length += delimiter.ToLiteral().Length + MeasureInlines(delimiter.FirstChild);
                        break;
                    case ContainerInline container:
                        length += MeasureInlines(container.FirstChild);
                        break;
                }
            }

            return length;
        }

        private static void WriteAsText(UIMarkdownRenderer renderer, List<TableRow> rows)
        {
            string indent = new(' ', renderer.IndentLevel * 2);
            foreach (TableRow row in rows)
            {
                renderer.WriteText(indent);
                if (row.IsHeader)
                {
                    renderer.WriteText("<b>");
                }

                for (int column = 0; column < row.Count; column++)
                {
                    if (column > 0)
                    {
                        renderer.WriteText(" | ");
                    }

                    if (row[column] is not TableCell cell)
                    {
                        continue;
                    }

                    foreach (Block block in cell)
                    {
                        if (block is LeafBlock leaf && leaf.Inline != null)
                        {
                            // Напрямую, без ParagraphBlockRenderer: он под блокировкой дописал бы перевод строки
                            // после каждой ячейки.
                            renderer.WriteLeafBlockInline(leaf);
                        }
                        else
                        {
                            renderer.Write(block);
                        }
                    }
                }

                if (row.IsHeader)
                {
                    renderer.WriteText("</b>");
                }

                renderer.WriteText("\n");
            }
        }
    }
}
