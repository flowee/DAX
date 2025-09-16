# DAX User-Defined Function for IBCS-guided visualizations
## [UDF_SVG_IBCS_AbsoluteValues](https://github.com/avatorl/DAX/blob/master/UDF/IBCS/UDF_SVG_IBCS_AbsoluteValues.dax)
<img width="267" height="150" alt="image" src="https://github.com/user-attachments/assets/75cc6271-17eb-4466-bbeb-91c0641c090d" />

```
UDF_SVG_IBCS_Absolutevariance (
    [Delta PY],
    FORMAT ( [Delta PY], "#0,," ),
    NOT ( HASONEVALUE ( 'Store'[Name] ) ),
    MAXX ( ALLSELECTED ( 'Store'[Short Name] ), [Delta PY] ),
    MINX ( ALLSELECTED ( 'Store'[Short Name] ), [Delta PY] ),
    MAXX ( ALLSELECTED ( 'Store' ), MAX ( [Sales AC], [Sales PY] ) )
)
```

## [UDF_SVG_IBCS_AbsoluteVariance](https://github.com/avatorl/DAX/blob/master/UDF/IBCS/UDF_SVG_IBCS_AbsoluteVariance.dax)
<img width="378" height="161" alt="image" src="https://github.com/user-attachments/assets/8cf32b8d-bc85-48b2-bab9-93b9b09f99c5" />

```
UDF_SVG_IBCS_Absolutevariance (
    [Delta PY],
    FORMAT ( [Delta PY], "#0,," ),
    NOT ( HASONEVALUE ( 'Store'[Name] ) ),
    MAXX ( ALLSELECTED ( 'Store'[Short Name] ), [Delta PY] ),
    MINX ( ALLSELECTED ( 'Store'[Short Name] ), [Delta PY] ),
    MAXX ( ALLSELECTED ( 'Store' ), MAX ( [Sales AC], [Sales PY] ) )
)
```

##

## [UDF_SVG_IBCS_RelativeVariance](https://github.com/avatorl/DAX/blob/master/UDF/IBCS/UDF_SVG_IBCS_RelativeVariance.dax)
<img width="290" height="150" alt="image" src="https://github.com/user-attachments/assets/c8a310e4-38d4-406d-8029-4f89bba522ad" />

```
UDF_SVG_IBCS_RelativeVariance (
    ROUND ( [Delta PY%] * 100, 0 ),
    FORMAT ( [Delta PY%] * 100, "+#,0;-#,0;#,0" ),
    NOT ( HASONEVALUE ( 'Store'[Name] ) )
)
```

