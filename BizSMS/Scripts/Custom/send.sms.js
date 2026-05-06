// GLOBALNI HANDLER (401)
//$(document).ajaxError(function (event, xhr) {

//    if (xhr.status === 401) {

//        // spreči duplo otvaranje popup-a
//        if (window.sessionExpiredHandled) return;
//        window.sessionExpiredHandled = true;

//        bootbox.alert({
//            message: "Vaša sesija je istekla. Molimo prijavite se ponovo.",
//            callback: function () {
//                window.location.href = '/Account/Login';
//            }
//        });
//    }
//});


//// SESSION WATCHER
//if (!window.sessionWatcherStarted) {

//    window.sessionWatcherStarted = true;

//    setInterval(function () {

//        $.ajax({
//            url: "/api/session/check",
//            method: "GET"
//        });

//    }, 30000);
//} // 30 sekundi
/* List */
$(function () {
    var move_right = '<span class="glyphicon glyphicon-minus pull-left  dual-list-move-right"></span>';
    var move_left = '<span class="glyphicon glyphicon-plus  pull-right dual-list-move-left"></span>';

    $('body').on('click', '.list-group .list-group-item', function () {
        $(this).toggleClass('active');
    });


    $('body').on('click', '.dual-list-move-right', function (e) {
        e.preventDefault();

        actives = $(this).parent();
        $(this).parent().find("span").remove();
        $(move_left).clone().appendTo(actives);
        actives.clone().appendTo('.list-right ul').removeClass("active");
        actives.remove();

        //sortUnorderedList("dual-list-right");

        enableNextButton();
        updateSelectedOptions();
    });


    $('body').on('click', '.dual-list-move-left', function (e) {
        e.preventDefault();

        actives = $(this).parent();
        $(this).parent().find("span").remove();
        $(move_right).clone().appendTo(actives);
        actives.clone().appendTo('.list-left ul').removeClass("active");
        actives.remove();

        enableNextButton();
        updateSelectedOptions();
    });


    $('.move-right, .move-left').click(function () {
        var $button = $(this), actives = '';
        if ($button.hasClass('move-left')) {
            actives = $('.list-right ul li.active');
            actives.find(".dual-list-move-left").remove();
            actives.append($(move_right).clone());
            actives.clone().appendTo('.list-left ul').removeClass("active");
            actives.remove();

        } else if ($button.hasClass('move-right')) {
            actives = $('.list-left ul li.active');
            actives.find(".dual-list-move-right").remove();
            actives.append($(move_left).clone());
            actives.clone().appendTo('.list-right ul').removeClass("active");
            actives.remove();
        }
        
        enableNextButton();
        updateSelectedOptions();
    });


    function enableNextButton() {

        var ulSelectedNumbers = $("#dual-list-left li");
        
        if (ulSelectedNumbers.length === 0) {
            $("#step-1-next")
                .attr("disabled", "disabled")
                .addClass("disabled");
        }
        else {
            $("#step-1-next")
                .removeAttr("disabled")
                .removeClass("disabled");
        }
    }

    function updateSelectedOptions() {
        $('#dual-list-options').find('option').remove();

        $('.list-left ul li').each(function (idx, opt) {
            $('#dual-list-options').append($("<option></option>")
                .attr("value", $(opt).data("value"))
                .text($(opt).text())
                .prop("selected", "selected")
            );
        });
    }


    $('.dual-list .selector').click(function () {
        var $checkBox = $(this);
        if (!$checkBox.hasClass('selected')) {
            $checkBox.addClass('selected').closest('.well').find('ul li:not(.active)').addClass('active');
            $checkBox.children('i').removeClass('glyphicon-unchecked').addClass('glyphicon-check');
        } else {
            $checkBox.removeClass('selected').closest('.well').find('ul li.active').removeClass('active');
            $checkBox.children('i').removeClass('glyphicon-check').addClass('glyphicon-unchecked');
        }
    });


    $('[name="SearchDualList"]').keyup(function (e) {
        var code = e.keyCode || e.which;
        if (code === '9') return;
        if (code === '27') $(this).val(null);
        var $rows = $(this).closest('.dual-list').find('.list-group li');
        var val = $.trim($(this).val()).replace(/ +/g, ' ').toLowerCase();
        $rows.show().filter(function () {
            var text = $(this).text().replace(/\s+/g, ' ').toLowerCase();
            return !~text.indexOf(val);
        }).hide();
    });


    $(".glyphicon-search").on("click", function () {
        $(this).next("input").focus();
    });


    function sortUnorderedList(ul, sortDescending) {
        $("#" + ul + " li").sort(sort_li).appendTo("#" + ul);

        function sort_li(a, b) {
            return $(b).data('value') < $(a).data('value') ? 1 : -1;
        }
    }

    $("#dual-list-left li").append(move_right);
    $("#dual-list-right li").append(move_left);

    //kada se klikne na prvi next hvata se id grupe (ako nije grupno slanje id ce biti null) ili pojedinacni brojevi ( ako je grupno slanje numbersToCheck ce biti null)
    $("#step-1-next").on("click", function () {

        var id = $("#group_select :selected").val();

        var numbersToCheck = $("#dual-list-left li").map(function () {
            return $(this).attr("data-number-id");
        }).get();

        //ako nije grupno slanje setovati izabranu grupu na null a ako jeste onda ponistiti brojeve za slanje
        if (!$("#send-sms-to-group").data('clicked')) {
            id = null;
        } else if ($("#send-sms-to-group").data('clicked')) {
            numbersToCheck = null;
        }        
        
        var dataRequest = {
            NumbersToCheck: numbersToCheck,
            GroupId: id
        };

        $.ajax({
            contentType: "application/json",
            method: "POST",
            data: JSON.stringify(dataRequest),
            url: "/api/home/CheckNonMtsGetAlphanums"
        })
            .done(function (data) {
                var alphanumericSelect = $("#alphanumericDropdown");
                var alphanumericVal = data[0]["Text"];
                var alphanumericValId = data[0]["Value"];
                $(".alphanumeric-for-send").text(alphanumericVal);
                $(".alphanumeric").text(alphanumericVal);
                alphanumericSelect.empty();

                $.each(data, function (key, value) {
                    alphanumericSelect.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
                });

                alphanumericSelect.on("change", function () {
                    var alphanumericVal = $(this).find("option:selected").text();
                    $(".alphanumeric-for-send").text(alphanumericVal);
                    $(".alphanumeric").text(alphanumericVal);
                });
            })
            .fail(function (data) {
                bootbox.alert({
                    message: data.responseJSON,
                    //ova opcija ispod je ako necu da se poruka prosledjuje sa Controllera vec ce se slati front-enda
                    //message: $("#step-1-next").attr("data-no-num-sender"),
                    callback: function () { location.reload(true); }
                })
            });
        
        //unsubscribe text
        $.ajax({
            url: "/api/home/GetUnsubTextMts/",
            method: "GET"
        })
            .done(function (data) {
                $("#inMts").text(data);
            })
            .fail(function () {
                $("#inMts").text("ODJAVA poslati SMS na 6099: STOP  *****, 0RSD");
            });

        $.ajax({
            url: "/api/home/GetUnsubtextNotInMts/",
            method: "GET"
        })
            .done(function (data) {
                $("#notInMts").text(data);
            })
            .fail(function () {
                $("#notInMts").text("ODJAVA pozvati 0800200400 i ukucajte: *****, 0RSD");
            });
    });

    //Step-2
    var alphanumericSelect = $("#alphanumericDropdown");
    var message = $("#message");

    alphanumericSelect.on("change", function () {
        if ($(this).length === 0) {
            $("#step-2-next")
                .attr("disabled", "disabled")
                .addClass("disabled");
        }
        else {
            if (message.val().length !== 0)
                $("#step-2-next")
                    .removeAttr("disabled")
                    .removeClass("disabled");
        }
    });

    message.on("keyup", function (char) {
        if ($(this).val().length === 0) {
            $("#step-2-next")
                .attr("disabled", "disabled")
                .addClass("disabled");
        }
        else {
            if(alphanumericSelect.length !== 0)
            $("#step-2-next")
                .removeAttr("disabled")
                .removeClass("disabled");
        }
       
        var message = $("#message").val();
        if (checkMessageCoding(message) === 1) {
            $('#message').attr('maxlength', '315');
            $("#charCount").text(message.length + "/66 (" + Math.ceil(message.length / 66) + ")");
        }
        else {
            $('#message').attr('maxlength', '765');
            $("#charCount").text(message.length + "/160 (" + Math.ceil(message.length / 160) + ")");
        }
    });

    function checkMessageCoding(message) {
        for (var i = 0; i < message.length; i++) {
            if (message.charCodeAt(i) > 127) {
                return 1;
            }
        }
        return 0;
    }

    //Wizard
    var navListItems = $('div.setup-panel div a'),
        allWells = $('.setup-content'),
        allNextBtn = $('.nextBtn'),
        allPrevBtn = $('.prevBtn');
    allWells.hide();

    navListItems.click(function (e) {
        e.preventDefault();
        var $target = $($(this).attr('href')),
            $item = $(this);

        if (!$item.hasClass('disabled')) {
            allWells.hide();
            $target.show();
            $target.find('input:eq(0)').focus();
            $item.addClass('btn-info');
        }
    });

    allNextBtn.click(function () {

        var disabled = $(this).attr("disabled");

        if (disabled === "disabled")
            return;

        var curStep = $(this).closest(".setup-content"),
            curStepBtn = curStep.attr("id"),
            nextStepWizard = $('div.setup-panel div a[href="#' + curStepBtn + '"]').parent().next().children("a"),
            curInputs = curStep.find("input[type='text'],input[type='url']"),
            isValid = true;

        $(".form-group").removeClass("has-error");
        for (var i = 0; i < curInputs.length; i++) {
            if (!curInputs[i].validity.valid) {
                isValid = false;
                $(curInputs[i]).closest(".form-group").addClass("has-error");
            }
        }

        if (isValid) {
            nextStepWizard.removeAttr('disabled')
                .removeClass('disabled')
                .removeClass('btn-default')
                .trigger('click');
            $('div.setup-panel div a[href="#' + curStepBtn + '"]')
                .attr('disabled', 'disabled')
                .removeClass('btn-primary')
                .removeClass('btn-info')
                .addClass('disabled')
                .addClass('btn-success');
        }
    });

    allPrevBtn.click(function () {
        var curStep = $(this).closest(".setup-content"),
            curStepBtn = curStep.attr("id"),
            prevStepWizard = $('div.setup-panel div a[href="#' + curStepBtn + '"]').parent().prev().children("a"),
            curInputs = curStep.find("input[type='text'],input[type='url']"),
            isValid = true;
        $(".form-group").removeClass("has-error");
        for (var i = 0; i < curInputs.length; i++) {
            if (!curInputs[i].validity.valid) {
                isValid = false;
                $(curInputs[i]).closest(".form-group").addClass("has-error");
            }
        }

        if (isValid) {
            prevStepWizard.removeAttr('disabled')
                .removeClass('disabled')
                .removeClass('btn-default')
                .trigger('click');
            $('div.setup-panel div a[href="#' + curStepBtn + '"]')
                .attr('disabled', 'disabled')
                .removeClass('btn-primary')
                .removeClass('btn-info')
                .removeClass('btn-success')
                .addClass('disabled')
                .addClass('btn-primary');
        }
    });
    $('div.setup-panel div a[href="#step-1"]').trigger("click");

    $("#send-sms-to-group").on("click", function () {
        $("#send-sms-to-group").data('clicked', true);

        //Group sending is chosen and next step is enabled
        $("#step-1-next")
            .removeAttr("disabled")
            .removeClass("disabled");
        $("#step-1-next").click();
    });

    $("#prevBtn-step-2").on("click", function () {
        
            $("#step-1-next")
                .attr("disabled", "disabled")
                .addClass("disabled");
        $("#send-sms-to-group").data('clicked', false);
    });

    //Group list
    $.ajax({
        url: "/api/home/getgroups/",
        method: "GET"
    })
        .done(function (data) {
            var select = $("<select>").addClass("form-control col-centered").attr("id", "group_select"),
                optDefault = $("<optgroup>").attr("label", $("#groupDropDown").attr("data-default-group")),
                optCustom = $("<optgroup>").attr("label", $("#groupDropDown").attr("data-custom-group"));
            $.each(data, function (key, value) {
                if (value["Default"] === true) {
                    optDefault.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
                }
                else {
                    optCustom.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
                }
            });

            select.append(optDefault);
            select.append(optCustom);

            select.on("change", function () {
                
                //Numbers
                $.ajax({
                    url: "/api/home/getnumbers/" + this.value,
                    method: "GET"
                })
                    .done(function (data) {
                        
                        var ulNumbers = $("#dual-list-right"),
                            ulSelectedNumbers = $("#dual-list-left li"),
                            move_right = '<span class="glyphicon glyphicon-plus  pull-right dual-list-move-left " title="Add Selected"></span>';
                        //console.log(ulSelectedNumbers);
                        ulNumbers.empty();
                        var arr = ulSelectedNumbers.map(function () {
                            return $(this).attr("data-number-id");
                        }).get();
                        //console.log(arr);

                        data = data.filter(function (val) {
                            return arr.indexOf(val.NumberID + '') === -1;
                        });
                        //console.log(data);

                        $.each(data, function (key, value) {
                            ulNumbers.append($("<li>")
                                .addClass('list-group-item')
                                .attr("data-number-id", value["NumberID"])
                                .text(value["Number"] + (value["Name"] !== null ? " (" + value["Name"] + ")" : ""))
                                .append(move_right));
                        });

                        //Check if numbers exist in group
                        if (data.length !== 0) {
                            $("#send-sms-to-group")
                                .removeAttr("disabled", "disabled")
                                .removeClass("disabled");
                        }
                        else {
                            $("#send-sms-to-group")
                                .attr("disabled", "disabled")
                                .addClass("disabled");
                        }
                    })
                    //.fail(function (data) {
                    //    bootbox.alert($("#send-sms").attr("data-fail-default"));
                    .fail(function (xhr) {
                        if (xhr.status === 401) return;
                        bootbox.alert($("#send-sms").attr("data-fail-default"));
                    });
                //test number
                $.ajax({
                    url: "/api/home/gettestnumber/",
                    method: "GET"
                })
                    .done(function (data) {
                        $("#test-number").text(data);
                    })
                    //.fail(function (data) {
                   //    bootbox.alert($("#send-sms").attr("data-fail-default"));
                    .fail(function (xhr) {
                        if (xhr.status === 401) return;
                        bootbox.alert($("#send-sms").attr("data-fail-default"));
                    });
            });
            $("#groupDropDown").append(select);
            select.trigger("change");
        })
        //.fail(function (data) {
        //    bootbox.alert($("#send-sms").attr("data-fail-default"));
        .fail(function (xhr) {
            if (xhr.status === 401) return;
            bootbox.alert($("#send-sms").attr("data-fail-default"));
        });

    //message
    $("#message").on("change", function () {
        $(".message-to-send").html($(this).val().replace(/\n/g, "<br />"));
    });
    // send test sms
    $("#send-test-sms").on("click", function () {

        var testNumber = $("#test-number").text(),
            alphanumeric = $(".alphanumeric-for-send").text(),
            message = $("textarea").val(),
            messageLength = 0;

        if (checkMessageCoding(message) === 1) {
            messageLength = Math.ceil(message.length / 66);
        }
        else {
            messageLength = Math.ceil(message.length / 160);
        }

        var dataRequest = {
            PhoneNumber: testNumber, Alphanumeric: alphanumeric,
            Message: message, MessageLength: messageLength
        };
        //open modal spinner
        var sending = bootbox.dialog({
            message: $('.modal-content').html(),
            closeButton: false
        });

        $.ajax({
            contentType: "application/json",
            method: "POST",
            data: JSON.stringify(dataRequest),
            url: "/api/home/sendtestsms/"
        })
        .done(function (data) {
            var link = $("#send-test-sms");
            var timePassed = 0;
            var interval = setInterval(function () {
                if (timePassed === 5) {
                    clearInterval(interval);
                    sending.modal('hide');
                    bootbox.confirm({
                        message: link.attr("data-question"),
                        buttons: {
                            confirm: {
                                label: link.attr("data-yes"),
                                className: 'btn-primary'
                            },
                            cancel: {
                                label: link.attr("data-no"),
                                className: 'btn-default'
                            }
                        },
                        callback: function (result) {
                            
                            if (result) {
                                //$("#skip-test-sms").hide($("#skip-test-sms"));
                                $("#skip-test-sms")
                                    .addClass("disabled")
                                    .attr("disabled", "disabled");

                                $("#step-3-next")
                                    .removeAttr("disabled")
                                    .removeClass("disabled");
                            } else {
                                $("#prevButton").click();
                            }
                        }
                    });
                }
                else
                {
                    timePassed++;
                }
            }, 1000);
        })
        .fail(function (data) {
            sending.modal('hide');
            var link = $("#send-test-sms");
            bootbox.alert({
                message: link.attr("data-fail")
            });
        });
    });

    // skip test sms
    $("#skip-test-sms").on("click", function () {
        var disabled = $(this).attr("disabled");
        if (disabled === "disabled")
            return;
        $("#step-3-next").click();
    });

    //step-3-next button click
    $("#step-3-next").on("click", function () {
        if ($("#send-sms-to-group").data('clicked')) {
            $('div#show-numbers-to-send').hide();
            $('div#show-group-to-send').show();
            $('div#group-numbers strong').text($("#group_select :selected").text());
        } else {
            $('div#show-group-to-send').hide();
            $('div#show-numbers-to-send').show();
        }
        prepareForSending();
    });

    //send sms
    $("#send-sms").on("click", function () {
        
        //send sms to group
        if ($("#send-sms-to-group").data('clicked')) {

            var parameters = getDefaultParameters();

            parameters.groupId = $("#group_select :selected").val();
            
            if (checkMessageCoding(parameters.message) === 1) {
                parameters.messageLength = Math.ceil(parameters.message.length / 66);
            }
            else {
                parameters.messageLength = Math.ceil(parameters.message.length / 160);
            }

            var dataRequest = {
                GroupId: parameters.groupId,
                Alphanumeric: parameters.alphanumeric,
                Message: parameters.message,
                MessageLength: parameters.messageLength
            };

            $("#send-sms")
                .addClass("disabled")
                .attr("disabled", "disabled");
            $("#schedule-sms")
                .addClass("disabled")
                .attr("disabled", "disabled");

            $.ajax({
                contentType: "application/json",
                method: "POST",
                data: JSON.stringify(dataRequest),
                url: "/api/home/sendgroupsms/"
            })
                .done(function (data) {
                    if (data.message === "OK") {
                        bootbox.alert($("#send-sms").attr("data-done"), function () {
                            window.location.href = "/Home/Index";
                        });
                    } else {
                        bootbox.alert($("#send-sms").attr("data-fail"), function () {
                            window.location.href = "/Home/Index";
                        });
                    }
                })
                .fail(function (data) {
                    bootbox.alert($("#send-sms").attr("data-fail"), function () {
                        window.location.href = "/Home/Index";
                    });
                });
        } else {

            //send sms to selected numbers
            var parameters = getDefaultParameters();

            parameters.ulSelectedNumbers = $("#numbers-list ul li");

            if (checkMessageCoding(parameters.message) === 1) {
                parameters.messageLength = Math.ceil(parameters.message.length / 66);
            }
            else {
                parameters.messageLength = Math.ceil(parameters.message.length / 160);
            }

            var numbers = parameters.ulSelectedNumbers.map(function () {
                return $(this).attr("data-number-id");
            }).get();

            var dataRequest = {
                PhoneNumbers: numbers,
                Alphanumeric: parameters.alphanumeric,
                Message: parameters.message,
                MessageLength: parameters.messageLength
            };

            $("#send-sms")
                .addClass("disabled")
                .attr("disabled", "disabled");
            $("#schedule-sms")
                .addClass("disabled")
                .attr("disabled", "disabled");

            $.ajax({
                contentType: "application/json",
                method: "POST",
                data: JSON.stringify(dataRequest),
                url: "/api/home/sendsms/"
            })
                .done(function (data) {
                    if (data.message === "OK") {
                        bootbox.alert($("#send-sms").attr("data-done"), function () {
                            window.location.href = "/Home/Index";
                        });
                    } else {
                        bootbox.alert($("#send-sms").attr("data-fail"), function () {
                            window.location.href = "/Home/Index";
                        });
                    }
                })
                .fail(function (data) {
                    bootbox.alert($("#send-sms").attr("data-fail"), function () {
                        window.location.href = "/Home/Index";
                    });
                });
        }
    });

    $("#schedule-sms").on("click", function () {

        function BootboxContent() {
            var str = document.cookie;
            var n = str.search(/culture=en/i);
            if (n !== -1) {
                $.datetimepicker.setLocale('en-US');
                var frm_str = '<form id="some-form">'
                    + '<div class="form-group">'
                    + '<label for="date">Date</label>'
                    + '<input id="date" class="date span2 form-control input-sm" size="16" placeholder="DD.MM.YYYY hh:mm" type="text">'
                    + '<span id="dateValidation" class="field-validation-valid text-danger"></span>'
                    + '</div>'
                    + '</form>';
            } else {
                $.datetimepicker.setLocale('sr-YU');
                var frm_str = '<form id="some-form">'
                    + '<div class="form-group">'
                    + '<label for="date">Datum</label>'
                    + '<input id="date" class="date span2 form-control input-sm" size="16" placeholder="DD.MM.YYYY hh:mm" type="text">'
                    + '<span id="dateValidation" class="field-validation-valid text-danger"></span>'
                    + '</div>'
                    + '</form>';
            }

            var object = $('<div/>').html(frm_str).contents();

            //jezik podesen ranije u if-u
            //document.cookie == '_culture=en-us' ? $.datetimepicker.setLocale('en-US') : $.datetimepicker.setLocale('sr-YU');
            
            object.find('#date').datetimepicker({
                step: 5,
                format: 'd.m.Y H:i',
                formatTime: 'H:i',
                formatDate: 'd.m.Y'
            });

            return object;
        }

        //Show the datepicker in the bootbox
        bootbox.confirm({
            message: BootboxContent,
            title: $("#schedule-sms").text(),
            buttons: {
                cancel: {
                    label: $("#schedule-sms").attr("data-cancel"),
                    className: 'btn-primary'
                },
                confirm: {
                    label: $("#schedule-sms").attr("data-confirm"),
                    className: 'btn-success'
                }
            },
            callback: function (result) {
                if (result) {
                    var dtNow = new Date();
                    if ($("#date").val() === "") {
                        $("#dateValidation").text($("#send-sms").attr("data-required-field"));
                        return false;
                    }
                    var dt = $("#date").val().split(" ");
                    var d = dt[0].split(".");
                    var t = dt[1].split(":");
                    dt = new Date(d[2], d[1] - 1, d[0], t[0], t[1]);
                    if ($("#date").val() === "") {
                        $("#dateValidation").text($("#send-sms").attr("data-required-field"));
                        return false;
                    } else if (dt < dtNow) {
                        $("#dateValidation").text($("#send-sms").attr("data-wrong-date"));
                        return false;
                    }
                    else {
                        $("#dateValidation").text("");
                    }

                    //schedule group sms
                    if ($("#send-sms-to-group").data('clicked')) {
                        
                        var parameters = getDefaultParameters();

                        parameters.groupId = $("#group_select :selected").val();
                        parameters.scheduledDate = $("#date").val();

                        if (checkMessageCoding(parameters.message) === 1) {
                            parameters.messageLength = Math.ceil(parameters.message.length / 66);
                        }
                        else {
                            parameters.messageLength = Math.ceil(parameters.message.length / 160);
                        }

                        var dataRequest = {
                            GroupId: parameters.groupId,
                            ScheduledDateTime: parameters.scheduledDate,
                            Alphanumeric: parameters.alphanumeric,
                            Message: parameters.message,
                            MessageLength: parameters.messageLength
                        };

                        $("#send-sms")
                            .addClass("disabled")
                            .attr("disabled", "disabled");
                        $("#schedule-sms")
                            .addClass("disabled")
                            .attr("disabled", "disabled");

                        $.ajax({
                            contentType: "application/json",
                            method: "POST",
                            data: JSON.stringify(dataRequest),
                            url: "/api/home/sendgroupsms/"
                        })
                            .done(function (data) {
                                if (data.message === "OK") {
                                    bootbox.alert($("#send-sms").attr("data-done"), function () {
                                        window.location.href = "/Home/Index";
                                    });
                                } else {
                                    bootbox.alert($("#send-sms").attr("data-fail"), function () {
                                        window.location.href = "/Home/Index";
                                    });
                                }
                            })
                            .fail(function (data) {
                                var message = JSON.parse(data.responseText);
                                bootbox.alert(message.Message, function () {
                                    $("#send-sms")
                                        .removeClass("disabled")
                                        .removeAttr("disabled", "disabled");
                                    $("#schedule-sms")
                                        .removeClass("disabled")
                                        .removeAttr("disabled", "disabled");
                                });
                            });
                    } else {

                        //schedule selected numbers sms
                        var parameters = getDefaultParameters();

                        parameters.ulSelectedNumbers = $("#numbers-list ul li");
                        parameters.scheduledDate = $("#date").val();

                        if (checkMessageCoding(parameters.message) === 1) {
                            parameters.messageLength = Math.ceil(parameters.message.length / 66);
                        }
                        else {
                            parameters.messageLength = Math.ceil(parameters.message.length / 160);
                        }

                        var numbers = parameters.ulSelectedNumbers.map(function () {
                            return $(this).attr("data-number-id");
                        }).get();

                        var dataRequest = {
                            PhoneNumbers: numbers,
                            ScheduledDateTime: parameters.scheduledDate,
                            Alphanumeric: parameters.alphanumeric,
                            Message: parameters.message,
                            MessageLength: parameters.messageLength
                        };

                        $("#send-sms")
                            .addClass("disabled")
                            .attr("disabled", "disabled");
                        $("#schedule-sms")
                            .addClass("disabled")
                            .attr("disabled", "disabled");

                        $.ajax({
                            contentType: "application/json",
                            method: "POST",
                            data: JSON.stringify(dataRequest),
                            url: "/api/home/sendsms/"
                        })
                            .done(function (data) {
                                if (data.message === "OK") {
                                    bootbox.alert($("#send-sms").attr("data-done"), function () {
                                        window.location.href = "/Home/Index";
                                    });
                                } else {
                                    bootbox.alert($("#send-sms").attr("data-fail"), function () {
                                        window.location.href = "/Home/Index";
                                    });
                                }
                            })
                            .fail(function (data) {
                                var message = JSON.parse(data.responseText);
                                bootbox.alert(message.Message, function () {
                                    $("#send-sms")
                                        .removeClass("disabled")
                                        .removeAttr("disabled", "disabled");
                                    $("#schedule-sms")
                                        .removeClass("disabled")
                                        .removeAttr("disabled", "disabled");
                                });
                            });
                    }
                
                }
            }
        });
    });

    function getDefaultParameters() {
        var defaultParametersObject = {
            message: $("textarea").val(),
            alphanumeric: $(".alphanumeric").text(),
            messageLength: 0
        };

        return defaultParametersObject;
    }
    
    function prepareForSending() {

        $("#send-sms-to-group").data('clicked') ?
            $("#sms-or-groupSms strong").text($("#sms-or-groupSms").attr("data-sms-to-group")) :
            $("#sms-or-groupSms strong").text($("#sms-or-groupSms").attr("data-sms-to-numbers"));

        $("#numbers-list").empty();

        var ulSelectedNumbers = $("#dual-list-left").clone()
            .appendTo("#numbers-list")
            .attr("id", "")
            .find("span").remove();
    }

    //$.ajax({
            //    url: "/api/home/CheckForNonMtsInGroup/" + id,
            //    method: "GET"
            //})
            //    .done(function (data) {
            //        if (data !== "onlyMts") {
            //            console.log("notonlyMts");
            //        } else {
            //            console.log("onlyMts");
            //        }
            //    })
            //    .fail(function (data) {
            //        bootbox.alert({
            //            message: "opa",
            //            callback: function () { location.reload(true); }
            //        })
            //    });


//provera brojeva u grupi
//if ($("#send-sms-to-group").data('clicked')) {

//    var id = $("#group_select :selected").val();

//    $.ajax({
//        url: "/api/home/CheckNonMts/" + id,
//        method: "GET"
//    })
//        .done(function (data) {

//            //u grupi postoje nonMts brojevi
//            if (data === true) {

//                $.ajax({
//                    url: "/api/home/getnonmtsalphanumerics/",
//                    method: "GET"
//                })
//                    .done(function (data) {
//                        var alphanumericSelect = $("#alphanumericDropdown");
//                        var alphanumericVal = data[0]["Text"];
//                        var alphanumericValId = data[0]["Value"];
//                        $(".alphanumeric-for-send").text(alphanumericVal);
//                        $(".alphanumeric").text(alphanumericVal);
//                        alphanumericSelect.empty();

//                        $.each(data, function (key, value) {
//                            alphanumericSelect.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
//                        });

//                        alphanumericSelect.on("change", function () {
//                            var alphanumericVal = $(this).find("option:selected").text();
//                            $(".alphanumeric-for-send").text(alphanumericVal);
//                            $(".alphanumeric").text(alphanumericVal);
//                        });
//                    })
//                    .fail(function (data) {
//                        bootbox.alert($("#send-sms").attr("data-fail-default"));
//                    });
//                //u grupi su samo mts brojevi
//            } else {
//                $.ajax({
//                    url: "/api/home/getalphanumerics/",
//                    method: "GET"
//                })
//                    .done(function (data) {
//                        var alphanumericSelect = $("#alphanumericDropdown");
//                        var alphanumericVal = data[0]["Text"];
//                        var alphanumericValId = data[0]["Value"];
//                        $(".alphanumeric-for-send").text(alphanumericVal);
//                        $(".alphanumeric").text(alphanumericVal);
//                        alphanumericSelect.empty();

//                        $.each(data, function (key, value) {
//                            alphanumericSelect.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
//                        });

//                        alphanumericSelect.on("change", function () {
//                            var alphanumericVal = $(this).find("option:selected").text();
//                            $(".alphanumeric-for-send").text(alphanumericVal);
//                            $(".alphanumeric").text(alphanumericVal);
//                        });
//                    })
//                    .fail(function (data) {
//                        bootbox.alert($("#send-sms").attr("data-fail-default"));
//                    });
//            }
//        })
            
//});

    

        
//                .fail(function () {
//                    bootbox.alert({
//                        message: "Nisu mogli biti provereni tipovi broja na koje se salje poruka",
//                        callback: function () { location.reload(true); }
//                    })
//                });
//} else {
    //provera pojedinacnih brojeva
    //var numbersToCheck = $("#dual-list-left li").map(function () {
    //    return $(this).attr("data-number-id");
    //}).get();
    //console.log(numbersToCheck);
    //var nonMts = false;

    //var dataRequest = { NumbersToCheck: numbersToCheck };

    //$.ajax({
    //    contentType: "application/json",
    //    method: "POST",
    //    data: JSON.stringify(dataRequest),
    //    url: "/api/home/CheckForNonMtsNumbers"
    //})
    //    .done(function (data) {
    //        //u grupi postoje nonMts brojevi
    //        if (data !== "onlyMts") {
    //            console.log("not only mts");
    //            $.ajax({
    //                url: "/api/home/getnonmtsalphanumerics/",
    //                method: "GET"
    //            })
    //                .done(function (data) {
    //                    var alphanumericSelect = $("#alphanumericDropdown");
    //                    var alphanumericVal = data[0]["Text"];
    //                    var alphanumericValId = data[0]["Value"];
    //                    $(".alphanumeric-for-send").text(alphanumericVal);
    //                    $(".alphanumeric").text(alphanumericVal);
    //                    alphanumericSelect.empty();

    //                    $.each(data, function (key, value) {
    //                        alphanumericSelect.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
    //                    });

    //                    alphanumericSelect.on("change", function () {
    //                        var alphanumericVal = $(this).find("option:selected").text();
    //                        $(".alphanumeric-for-send").text(alphanumericVal);
    //                        $(".alphanumeric").text(alphanumericVal);
    //                    });
    //                })
    //                .fail(function (data) {
    //                    bootbox.alert($("#send-sms").attr("data-fail-default"));
    //                });
    //            //u grupi su samo mts brojevi
    //        } else {
    //            $.ajax({
    //                url: "/api/home/getalphanumerics/",
    //                method: "GET"
    //            })
    //                .done(function (data) {
    //                    var alphanumericSelect = $("#alphanumericDropdown");
    //                    var alphanumericVal = data[0]["Text"];
    //                    var alphanumericValId = data[0]["Value"];
    //                    $(".alphanumeric-for-send").text(alphanumericVal);
    //                    $(".alphanumeric").text(alphanumericVal);
    //                    alphanumericSelect.empty();

    //                    $.each(data, function (key, value) {
    //                        alphanumericSelect.append($("<option>").attr('value', value["Value"]).text(value["Text"]));
    //                    });

    //                    alphanumericSelect.on("change", function () {
    //                        var alphanumericVal = $(this).find("option:selected").text();
    //                        $(".alphanumeric-for-send").text(alphanumericVal);
    //                        $(".alphanumeric").text(alphanumericVal);
    //                    });
    //                })
    //                .fail(function (data) {
    //                    bootbox.alert($("#send-sms").attr("data-fail-default"));
    //                });
    //        }
    //    })
    //    .fail(function () {
    //        bootbox.alert({
    //            message: "Nisu mogli biti provereni tipovi broja na koje se salje poruka",
    //            callback: function () { location.reload(true); }
    //        })
    //    });      
})
