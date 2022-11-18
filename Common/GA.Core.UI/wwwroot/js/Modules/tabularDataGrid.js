/**
 * Initializes the ag-grid
 * @param {any} element // The html element for the grid
 * @param {any} existingGridOptions // The existing grid options to destroy (Optional)
 * @param {any} gridColumns // The grid columns definitions (Optional)
 * @returns {any} gridOptions // a GridOptions object
 */
export function gridInit(element, existingGridOptions, gridColumns) {
    if (!element) {
        console.log('No grid element provided');
        return false;
    }

    if (existingGridOptions != null && existingGridOptions.api != null) {
        existingGridOptions.api.destroy(); // See https://ag-grid.zendesk.com/hc/en-us/articles/360016033371-Create-and-destroy-grids
        console.log('Destroyed existing grid options');
    }

    const gridOptions = getGridOptions(gridColumns);

    // ReSharper disable ConstructorCallNotUsed
    new window.agGrid.Grid(element, gridOptions);
    // ReSharper restore ConstructorCallNotUsed

    gridOptions.api.hideOverlay();

    console.log(`Initialized generic data grid`);
    console.log(gridOptions);

    gridOptions.addRowDataAsync = async function (nodes) {
        if (nodes == null) {
            console.error('nodes cannot be null');

            return;
        }

        try {
            await gridOptions.api.applyTransactionAsync({ add: nodes });
        } catch (e) {
            console.error(e);
        }
    };

    gridOptions.autoSizeAll = function (skipHeader) {
        try {
            const allColumnIds = [];
            gridOptions.columnApi.getColumns().forEach((column) => {
                allColumnIds.push(column.getId());
            });

            gridOptions.columnApi.autoSizeColumns(allColumnIds, skipHeader);
        } catch (e) {
            console.error(e);
        }
    };

    gridOptions.goToTopNode = function () {
        let node = null;
        gridOptions.api.forEachNodeAfterFilterAndSort(aNode => {
            if (node == null && aNode.level === 0) node = aNode;
        });

        if (node != null) {
            const targetIndex = node.rowIndex;
            gridOptions.api.ensureIndexVisible(targetIndex, 'top');

            node.setSelected(true, true);
            return true;
        } else {
            return false;
        }
    };

    gridOptions.goToBottomNode = function () {
        let node = null;
        gridOptions.api.forEachNodeAfterFilterAndSort(aNode => {
            if (aNode.displayed === true) { node = aNode; }
        });

        if (node != null) {
            const targetIndex = node.rowIndex;
            console.log(`Target index: ${targetIndex}`)
            gridOptions.api.ensureIndexVisible(targetIndex, 'bottom');

            node.setSelected(true, true);
            return true;
        } else {
            console.log(`Row node not found`);
            return false;
        }
    };

    return gridOptions;
}

/* ========================================================================================= */

function getGridOptions(gridColumns) {

    const gridOptions =
    {
        columnTypes: getColumnTypes(),
        columnDefs: getColumnDefs(),
        defaultColDef: {
            sortable: true,
            resizable: true,
            editable: false,
            floatingFilter: true,
            icons: {
                sortAscending: '<i class="fa fa-sort-up"/>',
                sortDescending: '<i class="fa fa-sort-down"/>'
            }
        },
        rowSelection: 'single',
        rowStyle: { 'font-family': 'monospace' },
        animateRows: false,
        suppressCellSelection: false,
        overlayLoadingTemplate: '<i class="fa-4x fas fa-circle-notch fa-spin"></i>',
        sideBar: {
            toolPanels: [
                {
                    id: 'filters',
                    labelDefault: 'Filters',
                    labelKey: 'filters',
                    iconKey: 'filter',
                    toolPanel: 'agFiltersToolPanel',
                    toolPanelParams: {
                        suppressExpandAll: false,
                        suppressFilterSearch: false,
                    },
                },
                {
                    id: 'columns',
                    labelDefault: 'Columns',
                    labelKey: 'columns',
                    iconKey: 'columns',
                    toolPanel: 'agColumnsToolPanel',
                    minWidth: 225,
                    maxWidth: 225,
                    width: 225
                }
            ],
            defaultToolPanel: '',
        },
        statusBar: {
            statusPanels: [
                {
                    statusPanel: 'agTotalAndFilteredRowCountComponent',
                    align: 'left'
                }
            ]
        },
        components: {
            booleanCellRenderer: booleanCellRenderer,
            booleanFilterCellRenderer: booleanFilterCellRenderer,
            booleanFilter: getBooleanFilter()
        },
        onSortChanged: e => e.api.refreshCells()
    };

    return gridOptions;

    function getColumnTypes() {
        return {
            stringColumn: {
                icons: {
                    sortAscending: '<i class="fa fa-sort-alpha-up"/>',
                    sortDescending: '<i class="fa fa-sort-alpha-down"/>'
                },
                filter: 'agTextColumnFilter',
                filterParams: {
                    defaultOption: 'contains',
                    suppressAndOrCondition: true,
                    applyMiniFilterWhileTyping: true,
                    buttons: ['clear'],
                    trimInput: true,
                    filterOptions: [
                        'startsWith',
                        'contains',
                        'endsWith',
                        'equals',
                        'notEqual'
                    ]
                }
            },
            numberColumn: {
                filter: 'agNumberColumnFilter',
                filterParams: {
                    defaultOption: 'greaterThanOrEqual'
                },
                icons: {
                    sortAscending: '<i class="fa-solid fa-arrow-up-1-9"/>',
                    sortDescending: '<i class="fa-solid fa-arrow-down-9-1"/>'
                }
            },
            booleanColumn: {
                cellStyle: { "text-align": 'center' },
                cellRenderer: 'booleanCellRenderer',
                filter: 'booleanFilter',
                floatingFilter: false,
                icons: {
                    sortAscending: '<i class="fa fa-sort-up"/>',
                    sortDescending: '<i class="fa fa-sort-down"/>'
                }
            },
            currencyColumn: {
                cellClass: 'ag-right-aligned-cell',
                valueFormatter: params => currencyFormatter(params.value, '$'),
                filter: 'agNumberColumnFilter',
                filterParams: {
                    defaultOption: 'greaterThanOrEqual'
                },
                icons: {
                    sortAscending: '<i class="fa fa-sort-amount-up"/>',
                    sortDescending: '<i class="fa fa-sort-amount-down"/>'
                }
            },
            dateColumn: {
                filter: 'agDateColumnFilter',
                filterParams: dateFilterParams,
                valueFormatter: params => dateOnlyGetter(params.value),
                filterValueGetter: params => dateOnlyGetter(params.value),
                keyCreator: params => dateOnlyGetter(params.value),
                icons: {
                    sortAscending: '<i class="fa fa-sort-up"/>',
                    sortDescending: '<i class="fa fa-sort-down"/>'
                }
            }
        };
    }

    function getColumnDefs() {
        const columnDefs = [];
        columnDefs.push(getRowNumberColumnDef());
        if (gridColumns != null)
            gridColumns.forEach(gridColumn => {
                columnDefs.push(createColumnDef(gridColumn));
            });
        return columnDefs;

        function getRowNumberColumnDef() {
            return {
                headerName: '',
                headerComponentParams: {
                    template:
                        '<span style="color: gray; font-size: 9px" title="Row Number"><i class="fas fa-hashtag"></i></span>'
                },
                valueGetter: 'node.rowIndex + 1',
                width: 45,
                pinned: 'left',
                cellRenderer: params => `<span style="color: gray; font-size: 9px">${params.value}</i></span>`,
                filter: null
            };
        }

        function createColumnDef(gridColumn) {
            const columnDef = {
                field: gridColumn.name,
                type: getType(gridColumn.dataType),
                headerTooltip: `${gridColumn.name} : ${gridColumn.dataType}`,
                // enableRowGroup: true
            };

            return columnDef;

            function getType(dataType) {
                switch (dataType) {
                    case 'String':
                        return 'stringColumn';
                    case 'Decimal':
                        return 'currencyColumn';
                    case 'Int32':
                        return 'numberColumn';
                    case 'Boolean':
                        return 'booleanColumn';
                    case 'DateTime':
                        return 'dateColumn';
                }

                return 'stringColumn';
            }
        }
    }
};

function booleanCellRenderer(params) {
    const valueCleaned = booleanCleaner(params.value);

    if (valueCleaned === true) {
        return "<span title='true' class='ag-icon ag-icon-tick content-icon text-success'></span>";
    }

    if (valueCleaned === false) {
        return "<span title='false' class='ag-icon ag-icon-cross content-icon text-danger'></span>";
    }

    if (params.value !== null && params.value !== undefined) {
        return params.value.toString();
    }
    return null;
}

function booleanFilterCellRenderer(params) {
    const valueCleaned = booleanCleaner(params.value);

    if (valueCleaned === true) {
        return "<span title='true' class='ag-icon ag-icon-tick content-icon text-success'></span>";
    }

    if (valueCleaned === false) {
        return "<span title='false' class='ag-icon ag-icon-cross content-icon text-danger'></span>";
    }

    if (params.value === '(Select All)') {
        return params.value;
    }
    return '(unknown)';
}


function getBooleanFilter() {

    function booleanFilter() { }

    booleanFilter.prototype.init = function (params) {
        const uniqueId = Math.random();
        this.filterChangedCallback = params.filterChangedCallback;
        this.eGui = document.createElement('div');
        this.eGui.innerHTML =
            '<div style="position: relative; margin: 20px 10px 10px 10px; padding: 20px 10px 10px 10px; border: 1px solid lightgray; border-radius: 8px;">' +
            '<div><label><input type="radio" name="filter"' + uniqueId + ' id="cbNoFilter" style="margin-right: 5px;">No filter</input></label></div>' +
            '<div style="margin: 5px 0;"><label><input type="radio" name="filter"' + uniqueId + ' id="cbPositive" style="margin-right: 5px;"><span class="d-inline-block ag-icon ag-icon-tick content-icon text-success"></span></input></label></div>' +
            '<div style="margin: 5px 0;"><label><input type="radio" name="filter"' + uniqueId + ' id="cbNegative" style="margin-right: 5px;"><span class="d-inline-block ag-icon ag-icon-cross content-icon text-danger"></span></input></label></div>' +
            '<div style="margin: 5px 0;"><label><input type="radio" name="filter"' + uniqueId + ' id="cbUndefined" style="margin-right: 5px;">Blank</input></label></div>';
        this.cbNoFilter = this.eGui.querySelector('#cbNoFilter');
        this.cbPositive = this.eGui.querySelector('#cbPositive');
        this.cbNegative = this.eGui.querySelector('#cbNegative');
        this.cbUndefined = this.eGui.querySelector('#cbUndefined');
        this.cbNoFilter.checked = true; // initialise the first to checked
        this.cbNoFilter.onclick = this.filterChangedCallback;
        this.cbPositive.onclick = this.filterChangedCallback;
        this.cbNegative.onclick = this.filterChangedCallback;
        this.cbUndefined.onclick = this.filterChangedCallback;
        this.valueGetter = params.valueGetter;
    };

    booleanFilter.prototype.getGui = function () {
        this.eGui.querySelectorAll('div')[1].style.backgroundColor = 'transparent';
        return this.eGui;
    };

    booleanFilter.prototype.doesFilterPass = function (params) {
        const valueGetter = this.valueGetter;

        function getBooleanValue() {
            const value = valueGetter(params);
            const booleanValue = booleanCleaner(value);
            return booleanValue;
        }

        try {

            if (this.cbNoFilter.checked) { return true; }

            if (this.cbPositive.checked) { return getBooleanValue() === true; }
            if (this.cbNegative.checked) { return getBooleanValue() === false; }
            if (this.cbUndefined.checked) { return getBooleanValue() === null; }

            console.error('invalid checkbox selection');

        } catch (e) {
            console.error(e, e.stack);
        }

        return true;
    };

    booleanFilter.prototype.isFilterActive = function () {
        return !this.cbNoFilter.checked;
    };

    booleanFilter.prototype.getModelAsString = function (model) {
        return model ? model : '';
    };

    booleanFilter.prototype.getModel = function () {
        if (this.cbNoFilter.checked) { return ''; }
        if (this.cbPositive.checked) { return 'True'; }
        if (this.cbNegative.checked) { return 'False'; }
        if (this.cbUndefined.checked) { return '(Blank)'; }

        console.error('invalid checkbox selection');
    };

    booleanFilter.prototype.setModel = function () {
        // lazy, setModel() is not used
    };

    return booleanFilter;
}

function booleanCleaner(value) {
    if (value === undefined) {
        return null;
    }

    if (value === true || value === 1 || value === 'true' || value === 'True') {
        return true;
    }

    if (value === false || value === 0 || value === 'false' || value === 'False') {
        return false;
    }

    return null;
}

function currencyFormatter(currency, sign) {
    if (currency == null || typeof currency !== 'number') {
        return null;
    }

    const sansDec = currency.toFixed(0);
    const formatted = sansDec.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return sign + `${formatted}`;
}

const dateFilterParams = {
    defaultOption: 'equals',
    inRangeInclusive: true,
    browserDatePicker: true,
    minValidYear: 1980,
    comparator: function (filterLocalDateAtMidnight, cellValue) {

        const dateAsString = cellValue;
        if (dateAsString == null) {
            return 0;
        }

        // In the example application, dates are stored as dd/mm/yyyy
        // We create a Date object for comparison against the filter date
        const dateParts = dateAsString.split('-');
        const day = Number(dateParts[2]);
        const month = Number(dateParts[1]) - 1;
        const year = Number(dateParts[0]);
        const cellDate = new Date(year, month, day);

        // Now that both parameters are Date objects, we can compare
        if (cellDate < filterLocalDateAtMidnight) {
            return -1;
        } else if (cellDate > filterLocalDateAtMidnight) {
            return 1;
        }
        return 0;
    }
}

function dateOnlyGetter(date) {
    try {
        return dateOnly(date);
    } catch (e) {
        return null;
    }
}

function dateOnly(params) {
    if (typeof params === 'string') {
        const date = new Date(params);
        return date.toISOString().slice(0, 10);
    } else {
        return null;
    }
}