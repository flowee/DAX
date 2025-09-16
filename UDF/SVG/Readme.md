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

## [UDF_SVG.dax](https://github.com/avatorl/DAX/blob/master/UDF/SVG/UDF_SVG.dax)

```
UDF_SVG (
    "
    <rect x='0' y='0' width='100' height='100' fill='#ff0000' ></rect>
    <rect x='100' y='0' width='100' height='100' fill='#00ff00' ></rect>
    <rect x='200' y='0' width='100' height='100' fill='#0000ff' ></rect>
    ",
    300,
    300
)
```
