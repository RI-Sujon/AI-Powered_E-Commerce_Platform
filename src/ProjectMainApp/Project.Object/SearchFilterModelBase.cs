using System.Globalization;
using System.Text;

namespace Project.Object
{
    public class SearchFilterModelBase
    {
        public SearchModel? SearchModel { get; set; }
        public List<OrderByColumn>? OrderByColumns { get; set; }

        public const string NUMBER_COLUMN_VALUE_TYPE = "number";
        public const string DATE_TIME_COLUMN_VALUE_TYPE = "datetime";
        public const string DATE_COLUMN_VALUE_TYPE = "date";

        public string GetSearchFilterClause()
        {
            var whereClause = new StringBuilder();
            if (SearchModel != null)
            {
                if (SearchModel.ValueSearch != null &&
                    SearchModel.ValueSearch.SearchColumnList != null &&
                    SearchModel.ValueSearch.SearchColumnList.Count > 0 &&
                    !string.IsNullOrWhiteSpace(SearchModel.ValueSearch.SearchValue))
                {
                    var searchValue = SearchModel.ValueSearch.SearchValue;

                    string[] columnArray = getValueSearchExpression(SearchModel.ValueSearch.SearchColumnList, searchValue, SearchModel.ValueSearch.SearchValueType);

                    whereClause.Append("(");
                    whereClause.Append(string.Join(" OR ", columnArray));
                    whereClause.Append(")");

                }

                if (SearchModel.ColumnFilter != null && SearchModel.ColumnFilter.Count > 0)
                {
                    if (whereClause.Length > 0)
                    {
                        whereClause.Append(" AND ");
                    }

                    //for number type no need ''
                    var columnArray = SearchModel.ColumnFilter
                        .Select(getColumnExpression()).ToArray();

                    whereClause.Append("(");
                    whereClause.Append(string.Join(" AND ", columnArray));
                    whereClause.Append(")");

                }
            }

            return whereClause.ToString();

            static Func<Filter, string> getColumnExpression()
            {
                return c =>
                {
                    if (!string.IsNullOrEmpty(c.ColumnValueType) && c.ColumnValueType.Equals(NUMBER_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                        return $" {c.ColumnName} = {c.ColumnValue} ";

                    if (!string.IsNullOrEmpty(c.ColumnValueType) && c.ColumnValueType.Equals(DATE_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                        return $" {c.ColumnName} BETWEEN '{FormateToSqldate(c.ColumnValue)}' AND '{FormateToSqldate(c.ColumnValue, 1)}'";

                    if (!string.IsNullOrEmpty(c.ColumnValueType) && c.ColumnValueType.Equals(DATE_TIME_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                        return $" {c.ColumnName} BETWEEN '{FormateToSqldate(c.ColumnValue)}' AND '{FormateToSqldate(c.ColumnValue, 1)}'";

                    return $"UPPER({c.ColumnName}) like UPPER('%{c.ColumnValue}%') ";

                };
            }


            string[] getValueSearchExpression(List<string> serchColumnList, string searchValue, string? searchValueType)
            {
                if (!string.IsNullOrEmpty(searchValueType) && searchValueType.Equals(NUMBER_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                    return serchColumnList.Select(searchColumn => $" {searchColumn} = {searchValue}").ToArray();

                if (!string.IsNullOrEmpty(searchValueType) && searchValueType.Equals(DATE_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                    return serchColumnList.Select(searchColumn => $" {searchColumn} BETWEEN '{FormateToSqldate(searchValue)}' AND '{FormateToSqldate(searchValue, 1)}' ").ToArray();

                if (!string.IsNullOrEmpty(searchValueType) && searchValueType.Equals(DATE_TIME_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                    return serchColumnList.Select(searchColumn => $" {searchColumn} BETWEEN '{FormateToSqldate(searchValue)}' AND '{FormateToSqldate(searchValue, 1)}' ").ToArray();


                return serchColumnList.Select(searchColumn => $" UPPER({searchColumn}) like UPPER('%{searchValue}%') ").ToArray();
            }
        }

        private static string FormateToSqldate(string? ddMMYYYYDateString, int addOneDays = 0)
        {
            if (string.IsNullOrWhiteSpace(ddMMYYYYDateString))
                return string.Empty;

            DateTime? date = null;

            try
            {
                date = DateTime.ParseExact(ddMMYYYYDateString, "dd-MM-yyyy", CultureInfo.InvariantCulture);
            }
            catch
            {
                date = DateTime.ParseExact(ddMMYYYYDateString, "d-M-yyyy", CultureInfo.InvariantCulture);
            }

            if (addOneDays > 0)
            {
                date = date.Value.AddDays(addOneDays);
            }

            return date.HasValue ? date.Value.ToString("yyyy-MM-dd") : string.Empty;
        }

        public string GetSearchFilterClauseOData()
        {
            var whereClause = new StringBuilder();
            if (SearchModel != null)
            {
                if (SearchModel.ValueSearch != null &&
                    SearchModel.ValueSearch.SearchColumnList != null &&
                    SearchModel.ValueSearch.SearchColumnList.Count > 0 &&
                    !string.IsNullOrWhiteSpace(SearchModel.ValueSearch.SearchValue))
                {
                    var searchValue = SearchModel.ValueSearch.SearchValue;

                    string[] columnArray = getValueSearchExpressionOData(SearchModel.ValueSearch.SearchColumnList, searchValue, SearchModel.ValueSearch.SearchValueType);

                    whereClause.Append("(");
                    whereClause.Append(string.Join(" or ", columnArray));
                    whereClause.Append(")");

                }

                if (SearchModel.ColumnFilter != null && SearchModel.ColumnFilter.Count > 0)
                {
                    if (whereClause.Length > 0)
                    {
                        whereClause.Append(" and ");
                    }

                    //for number type no need ''
                    var columnArray = SearchModel.ColumnFilter
                        .Select(getColumnExpressionOData()).ToArray();

                    whereClause.Append("(");
                    whereClause.Append(string.Join(" and ", columnArray));
                    whereClause.Append(")");

                }
            }

            return whereClause.ToString();


            static Func<Filter, string> getColumnExpressionOData()
            {
                return c =>
                {
                    if (!string.IsNullOrEmpty(c.ColumnValueType) && c.ColumnValueType.Equals(NUMBER_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                        return $" contains({c.ColumnName} , {c.ColumnValue}) ";

                    if (!string.IsNullOrEmpty(c.ColumnValueType) && c.ColumnValueType.Equals(DATE_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                        return $" {c.ColumnName} BETWEEN '{FormateToSqldate(c.ColumnValue)}' AND '{FormateToSqldate(c.ColumnValue, 1)}' ";

                    if (!string.IsNullOrEmpty(c.ColumnValueType) && c.ColumnValueType.Equals(DATE_TIME_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                        return $" {c.ColumnName} BETWEEN '{FormateToSqldate(c.ColumnValue)}' AND '{FormateToSqldate(c.ColumnValue, 1)}' ";

                    return $" contains({c.ColumnName} , '{c.ColumnValue}') ";

                };
            }


            //--http://erp.cloudlabs.live:11148/POWERDIVBC/api/beta/itemCategories?company=APSCL&$skip=0&$top=20&$filter=contains(code,'PLANT') or contains(code,'OFFICE')
            string[] getValueSearchExpressionOData(List<string> serchColumnList, string searchValue, string? searchValueType)
            {
                if (!string.IsNullOrEmpty(searchValueType) && searchValueType.Equals(NUMBER_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                    return serchColumnList.Select(searchColumn => $" contains({searchColumn} , {searchValue})").ToArray();

                if (!string.IsNullOrEmpty(searchValueType) && searchValueType.Equals(DATE_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                    return serchColumnList.Select(searchColumn => $" {searchColumn} BETWEEN '{FormateToSqldate(searchValue)}' AND '{FormateToSqldate(searchValue, 1)}' ").ToArray();

                if (!string.IsNullOrEmpty(searchValueType) && searchValueType.Equals(DATE_TIME_COLUMN_VALUE_TYPE, StringComparison.OrdinalIgnoreCase))
                    return serchColumnList.Select(searchColumn => $" {searchColumn} BETWEEN '{FormateToSqldate(searchValue)}' AND '{FormateToSqldate(searchValue, 1)}' ").ToArray();


                return serchColumnList.Select(searchColumn => $" contains({searchColumn} , '{searchValue}')").ToArray();
            }
        }
    }

    public class UserWiseSearchFilterModel
    {
        public SearchModel? SearchModel { get; set; }
        public List<OrderByColumn>? OrderByColumns { get; set; }
        public required List<string> HiddenColumns { get; set; }
        public required List<string> ColumnsPositioned { get; set; }
    }

}
