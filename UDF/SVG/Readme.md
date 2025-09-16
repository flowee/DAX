# DAX User-Defined Functions that Generate SVG images
(other than IBCS guided charts)

❗Change Data Category to "Image URL"

## [UDF_HTMLCSS.dax](https://github.com/avatorl/DAX/blob/master/UDF/SVG/UDF_HTMLCSS.dax)

```
UDF_HTMLCSS (
    "<h1>TEST</h1><p>HTML</p>",
    "
h1 {
  color: blue;
}
p  {
  color: red;
}
",
    300,
    300
)
```
