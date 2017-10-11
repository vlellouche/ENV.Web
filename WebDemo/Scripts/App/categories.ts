﻿import { Component, OnInit, Injectable } from '@angular/core';
import * as utils from './lib/utils';
import * as models from './models';

@Component({

    template: `
<h1>Categories</h1>
<data-grid [settings]="dataSettings"> </data-grid>


`
})

@Injectable()
export class Categories {

    dataSettings = new utils.DataSettings<models.category>(apiUrl + 'categories',{
        allowDelete:true,
        columnKeys: ["id", "categoryName", "description"],
        allowUpdate: true,
        allowInsert: true,
        get: { limit:100 },
        numOfColumnsInGrid:2
    });

}
const apiUrl = '/dataApi/';




